using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss.UnitTest.Core.Events;

public class EventListenerFactoryTest
{
    private readonly EventListenerFactory _eventListenerFactory;

    public EventListenerFactoryTest()
    {
        var repository = new InMemoryEventRepository();
        _eventListenerFactory = new EventListenerFactory(repository);

        repository.Publish([
            new EventEnvelope { Event = new TestEvent(1), Version = 0 },
            new EventEnvelope { Event = new TestEvent(2), Version = 1 },
            new EventEnvelope { Event = new TestEvent(1), Version = 2 },
        ]).AsTask().Wait();
    }

    [Fact]
    public async Task CanGetRootReadModel()
    {
        var rootReadModel = await _eventListenerFactory.UpdateTo(new TestRootReadModel(), 2);
        Assert.Equal(3, rootReadModel.GotEvents);
    }

    [Fact]
    public async Task CanGetReadModel()
    {
        var readModel = await _eventListenerFactory.UpdateTo(new TestReadModel { Id = 1 }, 2);
        Assert.Equal(2, readModel.GotEvents);
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

    private record TestEvent(int Id) : Event;
}