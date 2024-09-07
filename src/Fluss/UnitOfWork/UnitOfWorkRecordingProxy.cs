using System.Collections.Concurrent;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss;

public class UnitOfWorkRecordingProxy(IUnitOfWork impl) : IUnitOfWork
{
    public ValueTask<long> ConsistentVersion()
    {
        return impl.ConsistentVersion();
    }

    public IReadOnlyCollection<EventListener> ReadModels => impl.ReadModels;

    public List<EventListener> RecordedListeners { get; } = [];

    public ValueTask<IReadModel> GetReadModel(Type tReadModel, object? key, long? at = null)
    {
        return impl.GetReadModel(tReadModel, key, at);
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel>(long? at = null) where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        return Record(impl.GetReadModel<TReadModel>(at));
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key, long? at = null) where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        return Record(impl.GetReadModel<TReadModel, TKey>(key, at));
    }

    public ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel>(long? at = null) where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        return Record(impl.UnsafeGetReadModelWithoutAuthorization<TReadModel>(at));
    }

    public ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(TKey key, long? at = null) where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        return Record(impl.UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(key, at));
    }

    public ValueTask<IReadOnlyList<TReadModel>> GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null) where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new() where TKey : notnull
    {
        return Record(impl.GetMultipleReadModels<TReadModel, TKey>(keys, at));
    }

    public ValueTask<IReadOnlyList<TReadModel>> UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null) where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new() where TKey : notnull
    {
        return Record(impl.UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(keys, at));
    }

    public IUnitOfWork WithPrefilledVersion(long? version)
    {
        return impl.WithPrefilledVersion(version);
    }

    private async ValueTask<TReadModel> Record<TReadModel>(ValueTask<TReadModel> readModel) where TReadModel : EventListener
    {
        var result = await readModel;
        RecordedListeners.Add(result);
        return result;
    }

    private async ValueTask<IReadOnlyList<TReadModel>> Record<TReadModel>(ValueTask<IReadOnlyList<TReadModel>> readModel) where TReadModel : EventListener
    {
        var result = await readModel;
        RecordedListeners.AddRange(result);
        return result;
    }

    public IReadOnlyList<EventListenerTypeWithKeyAndVersion> GetRecordedListeners()
    {
        var eventListenerTypeWithKeyAndVersions = new List<EventListenerTypeWithKeyAndVersion>();

        foreach (var recordedListener in RecordedListeners)
        {
            eventListenerTypeWithKeyAndVersions.Add(new EventListenerTypeWithKeyAndVersion(
                recordedListener.GetType(),
                recordedListener is IEventListenerWithKey keyListener ? keyListener.Id : null,
                recordedListener.LastAcceptedEvent
                ));
        }

        return eventListenerTypeWithKeyAndVersions;
    }

    public record EventListenerTypeWithKeyAndVersion(Type Type, object? Key, long Version)
    {
        public async ValueTask<bool> IsStillUpToDate(IUnitOfWork unitOfWork, long? at = null)
        {
            var readModel = await unitOfWork.GetReadModel(Type, Key, at);

            if (readModel is EventListener eventListener)
            {
                return eventListener.LastAcceptedEvent <= Version;
            }

            return false;
        }
    }

    public void Dispose()
    {
        impl.Dispose();

        GC.SuppressFinalize(this);
    }
}