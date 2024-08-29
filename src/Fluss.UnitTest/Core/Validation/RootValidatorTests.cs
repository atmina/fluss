using Fluss.Aggregates;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.Validation;
using Moq;

namespace Fluss.UnitTest.Core.Validation;

public class RootValidatorTests
{
    private readonly Mock<IArbitraryUserUnitOfWorkCache> _arbitraryUserUnitOfWorkCacheMock = new(MockBehavior.Strict);
    private readonly Mock<IWriteUnitOfWork> _unitOfWorkMock = new(MockBehavior.Strict);

    public RootValidatorTests()
    {
        _arbitraryUserUnitOfWorkCacheMock.Setup(c => c.GetUserUnitOfWork(It.IsAny<Guid>()))
            .Returns(() => _unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(u => u.WithPrefilledVersion(It.IsAny<long>()))
            .Returns(() => _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ValidatesValidEvent()
    {
        var validator = new RootValidator(
            _arbitraryUserUnitOfWorkCacheMock.Object,
            [new AggregateValidatorAlwaysValid()],
            [new EventValidatorAlwaysValid()]
        );

        await validator.ValidateEvent(new EventEnvelope { Event = new TestEvent() });
    }

    [Fact]
    public async Task ValidatesInvalidEvent()
    {
        var validator = new RootValidator(
            _arbitraryUserUnitOfWorkCacheMock.Object,
            [new AggregateValidatorAlwaysValid()],
            [new EventValidatorAlwaysInvalid()]
        );

        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await validator.ValidateEvent(new EventEnvelope { Event = new TestEvent() });
        });
    }

    [Fact]
    public async Task ValidatesValidAggregate()
    {
        var validator = new RootValidator(
            _arbitraryUserUnitOfWorkCacheMock.Object,
            [new AggregateValidatorAlwaysValid()],
            [new EventValidatorAlwaysValid()]
        );

        await validator.ValidateAggregate(new TestAggregate(), new Fluss.UnitOfWork(null!, null!, null!, null!, null!));
    }

    [Fact]
    public async Task ValidatesInvalidAggregate()
    {
        var validator = new RootValidator(
            _arbitraryUserUnitOfWorkCacheMock.Object,
            [new AggregateValidatorAlwaysInvalid()],
            [new EventValidatorAlwaysValid()]
        );

        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await validator.ValidateAggregate(new TestAggregate(), new Fluss.UnitOfWork(null!, null!, null!, null!, null!));
        });
    }

    private class TestEvent : Event;

    private class EventValidatorAlwaysValid : EventValidator<TestEvent>
    {
        public ValueTask Validate(TestEvent @event, IUnitOfWork unitOfWorkBeforeEvent)
        {
            return ValueTask.CompletedTask;
        }
    }

    private class EventValidatorAlwaysInvalid : EventValidator<TestEvent>
    {
        public ValueTask Validate(TestEvent @event, IUnitOfWork unitOfWorkBeforeEvent)
        {
            throw new Exception("Invalid");
        }
    }

    private record TestAggregate : AggregateRoot
    {
        protected override AggregateRoot When(EventEnvelope envelope)
        {
            return this;
        }
    }

    private class AggregateValidatorAlwaysValid : AggregateValidator<TestAggregate>
    {
        public ValueTask ValidateAsync(TestAggregate aggregateAfterEvent, IUnitOfWork unitOfWorkBeforeEvent)
        {
            return ValueTask.CompletedTask;
        }
    }

    private class AggregateValidatorAlwaysInvalid : AggregateValidator<TestAggregate>
    {
        public ValueTask ValidateAsync(TestAggregate aggregateAfterEvent, IUnitOfWork unitOfWorkBeforeEvent)
        {
            throw new Exception("Invalid");
        }
    }
}