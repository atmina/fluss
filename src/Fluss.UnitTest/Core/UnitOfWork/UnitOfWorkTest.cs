using Fluss.Aggregates;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.ReadModel;
using Fluss.Validation;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Fluss.UnitTest.Core.UnitOfWork;

public partial class UnitOfWorkTest
{
    private readonly InMemoryEventRepository _eventRepository;
    private readonly List<Policy> _policies;
    private readonly UnitOfWorkFactory _unitOfWorkFactory;
    private Mock<IRootValidator> _validator;

    public UnitOfWorkTest()
    {
        _eventRepository = new InMemoryEventRepository();
        _policies = [];

        _validator = new(MockBehavior.Strict);
        _validator.Setup(v => v.ValidateEvent(It.IsAny<IUnitOfWork>(), It.IsAny<EventEnvelope>()))
            .Returns<IUnitOfWork, EventEnvelope>((_, _) => Task.CompletedTask);
        _validator.Setup(v => v.ValidateAggregate(It.IsAny<AggregateRoot>(), It.IsAny<Fluss.UnitOfWork>()))
            .Returns<AggregateRoot, Fluss.UnitOfWork>((_, _) => Task.CompletedTask);

        _eventRepository.Publish([
            new EventEnvelope { Event = new TestEvent(1), Version = 0 },
            new EventEnvelope { Event = new TestEvent(2), Version = 1 },
            new EventEnvelope { Event = new TestEvent(1), Version = 2 },
        ]).AsTask().Wait();

        _unitOfWorkFactory = new UnitOfWorkFactory(
            new ServiceCollection()
                .AddScoped(_ => GetUnitOfWork())
                .BuildServiceProvider());
    }

    private Fluss.UnitOfWork GetUnitOfWork()
    {
        return Fluss.UnitOfWork.Create(
            _eventRepository,
            new EventListenerFactory(_eventRepository),
            _policies,
            new UserIdProvider(_ => Guid.NewGuid(), null!),
            _validator.Object
        );
    }

    [Fact]
    public async Task CanGetConsistentVersion()
    {
        Assert.Equal(2, await GetUnitOfWork().ConsistentVersion());
    }

    [Fact]
    public async Task CanGetAggregate()
    {
        var existingAggregate = await GetUnitOfWork().GetAggregate<TestAggregate, int>(1);
        Assert.True(existingAggregate.Exists);

        var notExistingAggregate = await GetUnitOfWork().GetAggregate<TestAggregate, int>(100);
        Assert.False(notExistingAggregate.Exists);
    }

    [Fact]
    public async Task CanPublish()
    {
        _policies.Add(new AllowAllPolicy());
        var unitOfWork = GetUnitOfWork();
        var notExistingAggregate = await unitOfWork.GetAggregate<TestAggregate, int>(100);
        await notExistingAggregate.Create();
        await unitOfWork.CommitInternal();

        var latestVersion = await _eventRepository.GetLatestVersion();
        var newEvent = await _eventRepository.GetEvents(latestVersion - 1, latestVersion).ToFlatEventList();
        Assert.Equal(new TestEvent(100), newEvent.First().Event);
    }

    [Fact]
    public async Task ThrowsWhenPublishNotAllowed()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            var notExistingAggregate = await GetUnitOfWork().GetAggregate<TestAggregate, int>(100);
            await notExistingAggregate.Create();
            await GetUnitOfWork().CommitInternal();
        });
    }

    [Fact]
    public async Task CanGetAggregateTwice()
    {
        _policies.Add(new AllowAllPolicy());
        var unitOfWork = GetUnitOfWork();
        var notExistingAggregate = await unitOfWork.GetAggregate<TestAggregate, int>(100);
        await notExistingAggregate.Create();
        var existingAggregate = await unitOfWork.GetAggregate<TestAggregate, int>(100);
        Assert.True(existingAggregate.Exists);
    }

    [Fact]
    public async Task CanGetRootReadModel()
    {
        _policies.Add(new AllowAllPolicy());

        var rootReadModel = await GetUnitOfWork().GetReadModel<TestRootReadModel>();
        Assert.Equal(3, rootReadModel.GotEvents);
    }

    [Fact]
    public async Task CanGetRootReadModelUnsafe()
    {
        var rootReadModel = await GetUnitOfWork().UnsafeGetReadModelWithoutAuthorization<TestRootReadModel>();
        Assert.Equal(3, rootReadModel.GotEvents);
    }

    [Fact]
    public async Task ThrowsWhenRootReadModelNotAuthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await GetUnitOfWork().GetReadModel<TestRootReadModel>();
        });
    }

    [Fact]
    public async Task CanGetReadModel()
    {
        _policies.Add(new AllowAllPolicy());

        var readModel = await GetUnitOfWork().GetReadModel<TestReadModel, int>(1);
        Assert.Equal(2, readModel.GotEvents);
    }

    [Fact]
    public async Task CanGetReadModelUnsafe()
    {
        var readModel = await GetUnitOfWork().UnsafeGetReadModelWithoutAuthorization<TestReadModel, int>(1);
        Assert.Equal(2, readModel.GotEvents);
    }

    [Fact]
    public async Task ThrowsWhenReadModelNotAuthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await GetUnitOfWork().GetReadModel<TestReadModel, int>(1);
        });
    }

    [Fact]
    public async Task CanGetMultipleReadModels()
    {
        _policies.Add(new AllowAllPolicy());


        var unitOfWork = GetUnitOfWork();
        var readModels = await unitOfWork.GetMultipleReadModels<TestReadModel, int>([1, 2]);
        Assert.Equal(2, readModels[0].GotEvents);
        Assert.Equal(1, readModels[1].GotEvents);
        Assert.Equal(2, readModels.Count);

        Assert.Equal(2, unitOfWork.ReadModels.Count);
        Assert.Contains(unitOfWork.ReadModels, rm => rm == readModels[0]);
        Assert.Contains(unitOfWork.ReadModels, rm => rm == readModels[1]);
    }


    [Fact]
    public async Task ReturnsNothingWhenMultipleReadModelNotAuthorized()
    {
        var readModels = await GetUnitOfWork().GetMultipleReadModels<TestReadModel, int>([1, 2]);
        Assert.Equal(0, readModels.Count(rm => rm != null));
    }

    [Fact]
    public async Task CanGetMultipleReadModelsUnsafe()
    {
        var readModels = await GetUnitOfWork().UnsafeGetMultipleReadModelsWithoutAuthorization<TestReadModel, int>([1, 2]);
        Assert.Equal(2, readModels[0].GotEvents);
        Assert.Equal(1, readModels[1].GotEvents);
        Assert.Equal(2, readModels.Count);
    }

    [Fact]
    public async Task CanCommitWithFactory()
    {
        _policies.Add(new AllowAllPolicy());

        await _unitOfWorkFactory.Commit(async unitOfWork =>
        {
            var aggregate = await unitOfWork.GetAggregate<TestAggregate, int>(100);
            await aggregate.Create();
        });

        Assert.Equal(3, await _eventRepository.GetLatestVersion());
    }

    [Fact]
    public async Task CanCommitAndReturnValueWithFactory()
    {
        _policies.Add(new AllowAllPolicy());

        var value = await _unitOfWorkFactory.Commit(async unitOfWork =>
        {
            var aggregate = await unitOfWork.GetAggregate<TestAggregate, int>(100);
            await aggregate.Create();

            return 42;
        });

        Assert.Equal(3, await _eventRepository.GetLatestVersion());
        Assert.Equal(42, value);
    }

    private record TestRootReadModel : RootReadModel
    {
        public int GotEvents { get; private init; }
        protected override TestRootReadModel When(EventEnvelope envelope)
        {
            return envelope.Event switch
            {
                TestEvent => this with { GotEvents = GotEvents + 1 },
                _ => this,
            };
        }
    }

    private record TestReadModel : ReadModelWithKey<int>
    {
        public int GotEvents { get; private init; }
        protected override TestReadModel When(EventEnvelope envelope)
        {
            return envelope.Event switch
            {
                TestEvent testEvent when testEvent.Id == Id => this with { GotEvents = GotEvents + 1 },
                _ => this,
            };
        }
    }

    private record TestAggregate : AggregateRoot<int>
    {
        public ValueTask Create()
        {
            return Apply(new TestEvent(Id));
        }

        protected override TestAggregate When(EventEnvelope envelope)
        {
            return envelope.Event switch
            {
                TestEvent testEvent when testEvent.Id == Id => this with { Exists = true },
                _ => this,
            };
        }
    }

    private record TestEvent(int Id) : Event;

    private class AllowAllPolicy : Policy
    {
        public ValueTask<bool> AuthenticateEvent(EventEnvelope envelope, IAuthContext authContext)
        {
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> AuthenticateReadModel(IReadModel readModel, IAuthContext authContext)
        {
            return ValueTask.FromResult(true);
        }
    }
}
