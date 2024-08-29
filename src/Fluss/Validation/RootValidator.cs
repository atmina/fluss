using System.Reflection;
using Fluss.Aggregates;
using Fluss.Authentication;
using Fluss.Events;

namespace Fluss.Validation;

public interface IRootValidator
{
    public Task ValidateEvent(EventEnvelope envelope, IReadOnlyList<EventEnvelope>? PreviousEnvelopes = null);
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
            if (!_aggregateValidators.ContainsKey(aggregateType))
            {
                _aggregateValidators[aggregateType] = new List<(AggregateValidator, MethodInfo)>();
            }

            var method = validator.GetType().GetMethod(nameof(AggregateValidator<AggregateRoot>.ValidateAsync))!;
            _aggregateValidators[aggregateType].Add((validator, method));
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

    public async Task ValidateEvent(EventEnvelope envelope, IReadOnlyList<EventEnvelope>? previousEnvelopes = null)
    {
        var unitOfWork = _arbitraryUserUnitOfWorkCache.GetUserUnitOfWork(envelope.By ?? SystemUser.SystemUserGuid);

        var willBePublishedEnvelopes = previousEnvelopes ?? new List<EventEnvelope>();

        var versionedUnitOfWork = unitOfWork.WithPrefilledVersion(envelope.Version - willBePublishedEnvelopes.Count - 1);
        foreach (var willBePublishedEnvelope in willBePublishedEnvelopes)
        {
            versionedUnitOfWork.PublishedEventEnvelopes.Enqueue(willBePublishedEnvelope);
        }

        var type = envelope.Event.GetType();

        if (!_eventValidators.TryGetValue(type, out var validators)) return;

        try
        {
            var invocations = validators.Select(v =>
                v.handler.Invoke(v.validator, new object?[] { envelope.Event, versionedUnitOfWork }));

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
            var invocations = validator.Select(v => v.handler.Invoke(v.validator, new object?[] { aggregate, unitOfWork }));
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
 