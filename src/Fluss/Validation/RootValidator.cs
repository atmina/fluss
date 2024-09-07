using System.Reflection;
using Collections.Pooled;
using Fluss.Aggregates;
using Fluss.Authentication;
using Fluss.Events;

namespace Fluss.Validation;

public interface IRootValidator
{
    public Task ValidateEvent(IUnitOfWork unitOfWork, EventEnvelope envelope);
    public Task ValidateAggregate(AggregateRoot aggregate, UnitOfWork unitOfWork);
}

public class RootValidator : IRootValidator
{
    private readonly IArbitraryUserUnitOfWorkCache _arbitraryUserUnitOfWorkCache;
    private readonly Dictionary<Type, List<(AggregateValidator validator, MethodInfo handler)>> _aggregateValidators = new();
    private readonly Dictionary<Type, List<(EventValidator validator, MethodInfo handler)>> _eventValidators = new();

    public RootValidator(IArbitraryUserUnitOfWorkCache arbitraryUserUnitOfWorkCache, IEnumerable<AggregateValidator> aggregateValidators, IEnumerable<EventValidator> eventValidators)
    {
        _arbitraryUserUnitOfWorkCache = arbitraryUserUnitOfWorkCache;
        CacheAggregateValidators(aggregateValidators);
        CacheEventValidators(eventValidators);
    }

    private void CacheAggregateValidators(IEnumerable<AggregateValidator> validators)
    {
        foreach (var validator in validators)
        {
            var aggregateType = validator.GetType().GetInterface(typeof(AggregateValidator<>).Name)!.GetGenericArguments()[0];
            if (!_aggregateValidators.TryGetValue(aggregateType, out List<(AggregateValidator validator, MethodInfo handler)>? value))
            {
                value = new List<(AggregateValidator, MethodInfo)>();
                _aggregateValidators[aggregateType] = value;
            }

            var method = validator.GetType().GetMethod(nameof(AggregateValidator<AggregateRoot>.ValidateAsync))!;
            value.Add((validator, method));
        }
    }

    private void CacheEventValidators(IEnumerable<EventValidator> validators)
    {
        foreach (var validator in validators)
        {
            var eventType = validator.GetType().GetInterface(typeof(EventValidator<>).Name)!.GetGenericArguments()[0];
            if (!_eventValidators.TryGetValue(eventType, out var eventTypeValidators))
            {
                eventTypeValidators = new List<(EventValidator, MethodInfo)>();
                _eventValidators[eventType] = eventTypeValidators;
            }

            var method = validator.GetType().GetMethod(nameof(EventValidator<Event>.Validate))!;
            eventTypeValidators.Add((validator, method));
        }
    }

    public async Task ValidateEvent(IUnitOfWork unitOfWork, EventEnvelope envelope)
    {
        var type = envelope.Event.GetType();

        if (!_eventValidators.TryGetValue(type, out var validators)) return;

        try
        {
            var invocations = validators.Select(v =>
                v.handler.Invoke(v.validator, [envelope.Event, unitOfWork]));

            await Task.WhenAll(invocations.Cast<ValueTask>().Select(async x => await x));
        }
        catch (TargetInvocationException targetInvocationException)
        {
            if (targetInvocationException.InnerException is { } innerException)
            {
                throw innerException;
            }

            throw;
        }
    }

    public async Task ValidateAggregate(AggregateRoot aggregate, UnitOfWork unitOfWork)
    {
        var type = aggregate.GetType();

        if (!_aggregateValidators.TryGetValue(type, out var validator)) return;

        try
        {
            var invocations = validator.Select(v => v.handler.Invoke(v.validator, [aggregate, unitOfWork]));
            await Task.WhenAll(invocations.Cast<ValueTask>().Select(async x => await x));
        }
        catch (TargetInvocationException targetInvocationException)
        {
            if (targetInvocationException.InnerException is { } innerException)
            {
                throw innerException;
            }

            throw;
        }
    }
}
