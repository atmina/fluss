using System.Collections.ObjectModel;

namespace Fluss.Events;

public interface IEventRepository
{
    event EventHandler NewEvents;
    ValueTask Publish(IEnumerable<EventEnvelope> events);
    ValueTask<ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>>> GetEvents(long fromExclusive, long toInclusive);
    ValueTask<IEnumerable<RawEventEnvelope>> GetRawEvents();
    ValueTask ReplaceEvent(long version, IEnumerable<RawEventEnvelope> newEvents);
    ValueTask<long> GetLatestVersion();
}

public interface IBaseEventRepository : IEventRepository;

public abstract class EventRepositoryPipeline : IEventRepository
{
    protected internal IEventRepository Next = null!;

    public virtual ValueTask<ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>>> GetEvents(long fromExclusive,
        long toInclusive)
    {
        return Next.GetEvents(fromExclusive, toInclusive);
    }

    public virtual ValueTask ReplaceEvent(long version, IEnumerable<RawEventEnvelope> newEvents)
    {
        return Next.ReplaceEvent(version, newEvents);
    }

    public virtual ValueTask<IEnumerable<RawEventEnvelope>> GetRawEvents()
    {
        return Next.GetRawEvents();
    }

    public virtual ValueTask<long> GetLatestVersion()
    {
        return Next.GetLatestVersion();
    }

    public virtual event EventHandler NewEvents
    {
        add => Next.NewEvents += value;
        remove => Next.NewEvents -= value;
    }

    public virtual ValueTask Publish(IEnumerable<EventEnvelope> events)
    {
        return Next.Publish(events);
    }
}
