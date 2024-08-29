using System.Collections.Concurrent;
using Fluss.Aggregates;
using Fluss.Events;

namespace Fluss.UnitOfWork;

public partial class UnitOfWork
{
    private readonly List<AggregateRoot> _aggregateRoots = new();
    public ConcurrentQueue<EventEnvelope> PublishedEventEnvelopes { get; } = new();

    public async ValueTask<TAggregate> GetAggregate<TAggregate>() where TAggregate : AggregateRoot, new()
    {
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.Aggregate", typeof(TAggregate).FullName);

        var aggregate = new TAggregate();

        aggregate = await _eventListenerFactory.UpdateTo(aggregate, await ConsistentVersion());

        aggregate = aggregate with { UnitOfWork = this };

        foreach (var publishedEventEnvelope in PublishedEventEnvelopes)
        {
            aggregate = (TAggregate)aggregate.WhenInt(publishedEventEnvelope);
        }

        _aggregateRoots.Add(aggregate);

        return aggregate;
    }

    public async ValueTask<TAggregate> GetAggregate<TAggregate, TKey>(TKey key)
        where TAggregate : AggregateRoot<TKey>, new()
    {
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.Aggregate", typeof(TAggregate).FullName);

        var aggregate = new TAggregate { Id = key };

        aggregate = await _eventListenerFactory.UpdateTo(aggregate, await ConsistentVersion());

        aggregate = aggregate with { UnitOfWork = this };

        foreach (var publishedEventEnvelope in PublishedEventEnvelopes)
        {
            aggregate = (TAggregate)aggregate.WhenInt(publishedEventEnvelope);
        }

        _aggregateRoots.Add(aggregate);

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
            Version = await ConsistentVersion() + PublishedEventEnvelopes.Count + 1
        };

        if (!await AuthorizeUsage(eventEnvelope))
        {
            throw new UnauthorizedAccessException(
                $"Cannot add event {eventEnvelope.Event.GetType()} as the current user.");
        }

        await ValidateEventResult(eventEnvelope, aggregate);

        PublishedEventEnvelopes.Enqueue(eventEnvelope);
    }

    private async ValueTask ValidateEventResult<T>(EventEnvelope envelope, T? aggregate) where T : AggregateRoot
    {
        if (aggregate is null) return;

        // It's possible that the given aggregate does not have all necessary events applied yet.
        aggregate = await UpdateAndApplyPublished(aggregate, null);

        var result = aggregate.WhenInt(envelope) as T;

        if (result == null || result == aggregate) return;

        await _validator.ValidateAggregate(result, this);
    }

    internal async ValueTask CommitInternal()
    {
        using var activity = FlussActivitySource.Source.StartActivity();

        var validatedEnvelopes = new List<EventEnvelope>();
        foreach (var envelope in PublishedEventEnvelopes)
        {
            await _validator.ValidateEvent(envelope, validatedEnvelopes);
            validatedEnvelopes.Add(envelope);
        }

        await _eventRepository.Publish(PublishedEventEnvelopes);
        _consistentVersion += PublishedEventEnvelopes.Count;
        PublishedEventEnvelopes.Clear();
    }

    private async ValueTask<TEventListener> UpdateAndApplyPublished<TEventListener>(TEventListener eventListener, long? at)
        where TEventListener : EventListener
    {
        eventListener = await _eventListenerFactory.UpdateTo(eventListener, at ?? await ConsistentVersion());

        foreach (var publishedEventEnvelope in PublishedEventEnvelopes)
        {
            if (eventListener.Tag.LastSeen < publishedEventEnvelope.Version)
            {
                eventListener = (TEventListener)eventListener.WhenInt(publishedEventEnvelope);
            }
        }

        return eventListener;
    }
}
