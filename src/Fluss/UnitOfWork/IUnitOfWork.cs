using System.Collections.Concurrent;
using Fluss.Aggregates;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss.UnitOfWork;

// Allows mocking
public interface IUnitOfWork
{
    ValueTask<TAggregate> GetAggregate<TAggregate, TKey>(TKey key)
        where TAggregate : AggregateRoot<TKey>, new();

    ValueTask Publish(Event @event, AggregateRoot? aggregate = null);
    ValueTask<long> ConsistentVersion();
    IReadOnlyCollection<EventListener> ReadModels { get; }
    ConcurrentQueue<EventEnvelope> PublishedEventEnvelopes { get; }

    ValueTask<TReadModel> GetReadModel<TReadModel>(long? at = null)
        where TReadModel : EventListener, IRootEventListener, IReadModel, new();

    ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key, long? at = null)
        where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new();

    ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel>(long? at = null)
        where TReadModel : EventListener, IRootEventListener, IReadModel, new();

    ValueTask<TReadModel>
        UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(TKey key, long? at = null)
        where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new();

    ValueTask<IReadOnlyList<TReadModel>>
        GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null) where TKey : notnull
        where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new();

    ValueTask<IReadOnlyList<TReadModel>>
        UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null)
        where TKey : notnull where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new();

    IUnitOfWork WithPrefilledVersion(long? version);
}
