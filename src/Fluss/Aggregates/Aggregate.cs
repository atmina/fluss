using Fluss.Events;

namespace Fluss.Aggregates;

public abstract record AggregateRoot : EventListener, IRootEventListener
{
    public UnitOfWork.UnitOfWork UnitOfWork { private get; init; } = null!;
    protected abstract override AggregateRoot When(EventEnvelope envelope);

    protected async ValueTask Apply(Event @event)
    {
        await UnitOfWork.Publish(@event, this);
    }
}

public abstract record AggregateRoot<TId> : AggregateRoot, IEventListenerWithKey<TId>
{
    public bool Exists { get; protected set; }
    public TId Id { get; init; } = default!;
}
