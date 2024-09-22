using System.Diagnostics.CodeAnalysis;
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

public interface IRootEventListener;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public interface IEventListenerWithKey
{
    public object Id { get; init; }
}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public interface IEventListenerWithKey<TKey> : IEventListenerWithKey
{
    public new TKey Id { get; init; }

    object IEventListenerWithKey.Id
    {
        get => Id!;
        init => Id = (TKey)value;
    }
}