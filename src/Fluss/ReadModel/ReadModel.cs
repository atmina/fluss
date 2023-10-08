using Fluss.Events;

namespace Fluss.ReadModel;

public interface IReadModel
{
}

public abstract record RootReadModel : EventListener, IRootEventListener, IReadModel
{
}

public abstract record ReadModelWithKey<TId> : EventListener, IEventListenerWithKey<TId>, IReadModel
{
    public virtual TId Id { get; init; } = default!;
}
