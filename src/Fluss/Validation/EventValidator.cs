using Fluss.Events;
using Fluss.UnitOfWork;

namespace Fluss.Validation;

public interface EventValidator { }

public interface EventValidator<in T> : EventValidator where T : Event
{
    ValueTask Validate(T @event, IUnitOfWork unitOfWorkBeforeEvent);
}
