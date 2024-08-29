using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;

namespace Fluss.Events;

public sealed class EventListenerFactory(IEventRepository eventRepository) : IEventListenerFactory
{
    public async ValueTask<TEventListener> UpdateTo<TEventListener>(TEventListener eventListener, long to) where TEventListener : EventListener
    {
        var events = await eventRepository.GetEvents(eventListener.Tag.LastSeen, to);

        return UpdateWithEvents(eventListener, events);
    }

    public TEventListener UpdateWithEvents<TEventListener>(TEventListener eventListener, ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> events) where TEventListener : EventListener
    {
        EventListener updatedEventListener = eventListener;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < events.Count; i++)
        {
            var eventSpan = events[i].Span;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var j = 0; j < eventSpan.Length; j++)
            {
                updatedEventListener = updatedEventListener.WhenInt(eventSpan[j]);
            }
        }

        return (TEventListener)updatedEventListener;
    }
}

public interface IEventListenerFactory
{
    [Pure]
    ValueTask<TEventListener> UpdateTo<TEventListener>(TEventListener eventListener, long to) where TEventListener : EventListener;

    TEventListener UpdateWithEvents<TEventListener>(TEventListener eventListener, ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> events)
        where TEventListener : EventListener;
}

public abstract class EventListenerFactoryPipeline : IEventListenerFactory
{
    protected internal IEventListenerFactory Next = null!;
    public abstract ValueTask<TEventListener> UpdateTo<TEventListener>(TEventListener eventListener, long to) where TEventListener : EventListener;
    public virtual TEventListener UpdateWithEvents<TEventListener>(TEventListener eventListener,
        ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> events) where TEventListener : EventListener
    {
        return Next.UpdateWithEvents(eventListener, events);
    }
}
