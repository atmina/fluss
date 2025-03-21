using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss;

public interface IUnitOfWork
{
    ValueTask<long> ConsistentVersion();
    IReadOnlyCollection<EventListener> ReadModels { get; }

    ValueTask<IReadModel> GetReadModel([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type tReadModel, object? key, long? at = null);

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

    ValueTask Return();
}
