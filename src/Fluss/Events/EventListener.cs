using Fluss.Events.TransientEvents;

namespace Fluss.Events;

public abstract record EventListener
{
    /// Last Event that was consumed by this EventListener
    internal long LastSeenEvent = -1;
    /// Last Event that mutated this EventListener
    internal long LastAcceptedEvent = -1;
    /// Last TransientEvent that was consumed by this EventListener
    internal long LastSeenTransientEvent = -1;

    protected abstract EventListener When(EventEnvelope envelope);

    internal EventListener WhenInt(EventEnvelope envelope)
    {
#if DEBUG
        if (envelope.Version != LastSeenEvent + 1 && envelope is not TransientEventEnvelope)
        {
            throw new InvalidOperationException(
                $"Event envelope version {envelope.Version} is not the next expected version {LastSeenEvent + 1}.");
        }
#endif

        var newEventListener = When(envelope);

        var changed = newEventListener != this;

        if (envelope.Event is TransientEvent)
        {
            newEventListener.LastSeenTransientEvent = envelope.Version;
            return newEventListener;
        }

        if (changed)
        {
            newEventListener.LastAcceptedEvent = envelope.Version;
        }

        newEventListener.LastSeenEvent = envelope.Version;

        return newEventListener;
    }

    internal bool HasTaint()
    {
        return LastSeenTransientEvent > -1;
    }
}

public record EventListenerVersionTag
{
    /// Last Event that was consumed by this EventListener
    public long LastSeen = -1;
    /// Last Event that mutated this EventListener
    public readonly long LastAccepted = -1;
    /// Last TransientEvent that was consumed by this EventListener
    public long LastSeenTransient = -1;

    public EventListenerVersionTag(long version, long lastSeenTransient = -1)
    {
        LastSeen = version;
        LastAccepted = version;
        LastSeenTransient = lastSeenTransient;
    }

    public bool HasTaint()
    {
        return LastSeenTransient > -1;
    }
}

public interface IRootEventListener;

public interface IEventListenerWithKey
{
    public object Id { get; }
}

public interface IEventListenerWithKey<TKey> : IEventListenerWithKey
{
    public new TKey Id { get; init; }

    object IEventListenerWithKey.Id => Id!;
}