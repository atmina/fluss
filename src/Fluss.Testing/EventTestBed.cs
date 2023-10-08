using Fluss.Events;
using Fluss.Extensions;

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
        var startingVersion = EventRepository.GetLatestVersion().GetAwaiter().GetResult();
        EventRepository.Publish(events.Select(@event => new EventEnvelope
        {
            Version = ++startingVersion,
            At = DateTimeOffset.Now,
            By = null,
            Event = @event
        })).GetAwaiter().GetResult();

        return this;
    }

    public virtual EventTestBed WithEventEnvelopes(params EventEnvelope[] eventEnvelopes)
    {
        EventRepository.Publish(eventEnvelopes).GetResult();
        return this;
    }
}
