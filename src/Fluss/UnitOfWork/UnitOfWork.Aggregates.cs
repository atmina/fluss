using System.Collections.Concurrent;
using Collections.Pooled;
using Fluss.Aggregates;
using Fluss.Events;
// ReSharper disable LoopCanBeConvertedToQuery

namespace Fluss;

public partial class UnitOfWork
{
    internal readonly PooledList<EventEnvelope> PublishedEventEnvelopes = [];

    public async ValueTask<TAggregate> GetAggregate<TAggregate>() where TAggregate : AggregateRoot, new()
    {
        EnsureInstantiated();
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.Aggregate", typeof(TAggregate).FullName);

        var aggregate = new TAggregate();

        aggregate = await _eventListenerFactory!.UpdateTo(aggregate, await ConsistentVersion());

        aggregate = aggregate with { UnitOfWork = this };

        foreach (var publishedEventEnvelope in PublishedEventEnvelopes)
        {
            aggregate = (TAggregate)aggregate.WhenInt(publishedEventEnvelope);
        }

        return aggregate;
    }

    public async ValueTask<TAggregate> GetAggregate<TAggregate, TKey>(TKey key)
        where TAggregate : AggregateRoot<TKey>, new()
    {
        EnsureInstantiated();
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.Aggregate", typeof(TAggregate).FullName);

        var aggregate = new TAggregate { Id = key };

        aggregate = await _eventListenerFactory!.UpdateTo(aggregate, await ConsistentVersion());

        aggregate = aggregate with { UnitOfWork = this };

        foreach (var publishedEventEnvelope in PublishedEventEnvelopes)
        {
            aggregate = (TAggregate)aggregate.WhenInt(publishedEventEnvelope);
        }

        return aggregate;
    }

    public async ValueTask Publish(Event @event, AggregateRoot? aggregate = null)
    {
        using var activity = FlussActivitySource.Source.StartActivity();

        var eventEnvelope = new EventEnvelope
        {
            At = DateTimeOffset.UtcNow,
            By = CurrentUserId(),
            Event = @event,
            Version = (_consistentVersion ?? await ConsistentVersion()) + PublishedEventEnvelopes.Count + 1
        };

        if (!await AuthorizeUsage(eventEnvelope))
        {
            throw new UnauthorizedAccessException(
                $"Cannot add event {eventEnvelope.Event.GetType()} as the current user.");
        }

        await ValidateEvent(eventEnvelope);
        await ValidateEventResult(eventEnvelope, aggregate);

        PublishedEventEnvelopes.Add(eventEnvelope);
    }

    private async Task ValidateEvent(EventEnvelope eventEnvelope)
    {
        EnsureInstantiated();

        await _validator!.ValidateEvent(this, eventEnvelope);
    }

    private async ValueTask ValidateEventResult<T>(EventEnvelope envelope, T? aggregate) where T : AggregateRoot
    {
        EnsureInstantiated();

        if (aggregate is null) return;

        // It's possible that the given aggregate does not have all necessary events applied yet.
        aggregate = await UpdateAndApplyPublished(aggregate, null);

        if (aggregate.WhenInt(envelope) is not T result || result == aggregate) return;

        await _validator!.ValidateAggregate(result, this);
    }

    internal async ValueTask CommitInternal()
    {
        using var activity = FlussActivitySource.Source.StartActivity();

        EnsureInstantiated();
        await _eventRepository!.Publish(PublishedEventEnvelopes);
        _consistentVersion += PublishedEventEnvelopes.Count;
        PublishedEventEnvelopes.Clear();
    }

    private async ValueTask<TEventListener> UpdateAndApplyPublished<TEventListener>(TEventListener eventListener, long? at)
        where TEventListener : EventListener
    {
        EnsureInstantiated();

        eventListener = await _eventListenerFactory!.UpdateTo(eventListener, at ?? await ConsistentVersion());

        foreach (var publishedEventEnvelope in PublishedEventEnvelopes)
        {
            if (eventListener.LastSeenEvent < publishedEventEnvelope.Version)
            {
                eventListener = (TEventListener)eventListener.WhenInt(publishedEventEnvelope);
            }
        }

        return eventListener;
    }
}
