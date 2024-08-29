using Fluss.Aggregates;

namespace Fluss.Validation;

public interface AggregateValidator { }

public interface AggregateValidator<in T> : AggregateValidator where T : AggregateRoot
{
    ValueTask ValidateAsync(T aggregateAfterEvent, Fluss.UnitOfWork.UnitOfWork unitOfWorkBeforeEvent);
}
