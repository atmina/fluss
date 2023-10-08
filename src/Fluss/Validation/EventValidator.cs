using Fluss.Events;

namespace Fluss.Core.Validation;

public interface EventValidator { }

public interface EventValidator<in T> : EventValidator where T : Event
{
    ValueTask Validate(T @event, Fluss.UnitOfWork.UnitOfWork unitOfWorkBeforeEvent);
}
