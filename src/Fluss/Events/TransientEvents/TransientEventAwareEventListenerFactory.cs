namespace Fluss.Events.TransientEvents;

public class TransientEventAwareEventListenerFactory : EventListenerFactoryPipeline
{
    private readonly TransientEventAwareEventRepository _transientEventRepository;

    public TransientEventAwareEventListenerFactory(TransientEventAwareEventRepository transientEventRepository)
    {
        _transientEventRepository = transientEventRepository;
    }

    public override async ValueTask<TEventListener> UpdateTo<TEventListener>(TEventListener eventListener, long to)
    {
        var next = await Next.UpdateTo(eventListener, to);
        var transientEvents = _transientEventRepository.GetCurrentTransientEvents();

        return UpdateWithEvents(next, transientEvents);
    }
}
