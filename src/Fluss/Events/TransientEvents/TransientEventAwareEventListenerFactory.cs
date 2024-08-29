namespace Fluss.Events.TransientEvents;

public class TransientEventAwareEventListenerFactory(TransientEventAwareEventRepository transientEventRepository)
    : EventListenerFactoryPipeline
{
    public override async ValueTask<TEventListener> UpdateTo<TEventListener>(TEventListener eventListener, long to)
    {
        var next = await Next.UpdateTo(eventListener, to);
        var transientEvents = transientEventRepository.GetCurrentTransientEvents();

        return UpdateWithEvents(next, transientEvents);
    }
}
