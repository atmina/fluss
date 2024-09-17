using System.Diagnostics.CodeAnalysis;
using Fluss.Aggregates;

namespace Fluss.Validation;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicMethods)]
public interface AggregateValidator;

public interface AggregateValidator<in T> : AggregateValidator where T : AggregateRoot
{
    ValueTask ValidateAsync(T aggregateAfterEvent, IUnitOfWork unitOfWorkBeforeEvent);
}
