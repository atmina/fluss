using System.Diagnostics.CodeAnalysis;
using Fluss.Events;

namespace Fluss.Validation;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicMethods)]
public interface EventValidator;

public interface EventValidator<in T> : EventValidator where T : Event
{
    ValueTask Validate(T @event, IUnitOfWork unitOfWorkBeforeEvent);
}
