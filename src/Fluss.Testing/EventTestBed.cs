using Fluss.Events;

namespace Fluss.Testing;

public abstract class EventTestBed
{
    protected readonly InMemoryEventRepository EventRepository;
    protected readonly EventListenerFactory EventListenerFactory;

    protected EventTestBed()
    {
        EventRepository = new InMemoryEventRepository();
        EventListenerFactory = new EventListenerFactory(EventRepository);
    }

    public virtual EventTestBed WithEvents(params Event[] events)
    {
        var startingVersion = EventRepository.GetLatestVersion().AsTask().Result;
        EventRepository.Publish(events.Select(@event => new EventEnvelope
        {
            Version = ++startingVersion,
            At = DateTimeOffset.Now,
            By = null,
            Event = @event
        })).AsTask().Wait();

        return this;
    }

    public virtual EventTestBed WithEventEnvelopes(params EventEnvelope[] eventEnvelopes)
    {
        EventRepository.Publish(eventEnvelopes).AsTask().Wait();
        return this;
    }
}
