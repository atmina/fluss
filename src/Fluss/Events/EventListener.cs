using Fluss.Events.TransientEvents;

namespace Fluss.Events;

public abstract record EventListener
{
    internal EventListenerVersionTag Tag = new(-1);
    protected abstract EventListener When(EventEnvelope envelope);

    internal EventListener WhenInt(EventEnvelope envelope)
    {
#if DEBUG
        if (envelope.Version != Tag.LastSeen + 1 && envelope is not TransientEventEnvelope)
        {
            throw new InvalidOperationException(
                $"Event envelope version {envelope.Version} is not the next expected version {Tag.LastSeen + 1}.");
        }
#endif

        var newEventListener = When(envelope);

        var changed = newEventListener != this;

        if (envelope.Event is TransientEvent)
        {
            if (newEventListener.Tag.HasTaint())
            {
                newEventListener.Tag.LastSeenTransient = envelope.Version;
            }
            else
            {
                newEventListener = newEventListener with { Tag = Tag with { LastSeenTransient = envelope.Version } };
            }

            return newEventListener;
        }

        if (changed)
        {
            newEventListener = newEventListener with { Tag = new EventListenerVersionTag(envelope.Version) };
        }
        else
        {
            newEventListener.Tag.LastSeen = envelope.Version;
        }

        return newEventListener;
    }
}

public record EventListenerVersionTag
{
    /// Last Event that was consumed by this EventListener
    public long LastSeen = -1;
    /// Last Event that mutated this EventListener
    public long LastAccepted = -1;
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

public interface IRootEventListener
{
}

public interface IEventListenerWithKey<TKey>
{
    public TKey Id { get; init; }
}