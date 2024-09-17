using System.Diagnostics.CodeAnalysis;
using Fluss.Events;

namespace Fluss.SideEffects;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicMethods)]
public interface SideEffect;

public interface SideEffect<in T> : SideEffect where T : Event
{
    public Task<IEnumerable<Event>> HandleAsync(T @event, UnitOfWork unitOfWork);
}
