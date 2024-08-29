using Fluss.Events;

namespace Fluss.SideEffects;

public interface SideEffect
{
}

public interface SideEffect<in T> : SideEffect where T : Event
{
    public Task<IEnumerable<Event>> HandleAsync(T @event, UnitOfWork unitOfWork);
}
