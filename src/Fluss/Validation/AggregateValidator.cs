using Fluss.Aggregates;
using Fluss.UnitOfWork;

namespace Fluss.Validation;

public interface AggregateValidator { }

public interface AggregateValidator<in T> : AggregateValidator where T : AggregateRoot
{
    ValueTask ValidateAsync(T aggregateAfterEvent, IUnitOfWork unitOfWorkBeforeEvent);
}
