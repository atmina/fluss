using System.Diagnostics.CodeAnalysis;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss;

public class AtNotAllowedInSelectorException : Exception;

public class UnitOfWorkRecordingProxy(IUnitOfWork impl) : IUnitOfWork
{
    public ValueTask<long> ConsistentVersion()
    {
        return impl.ConsistentVersion();
    }

    public IReadOnlyCollection<EventListener> ReadModels => impl.ReadModels;

    public List<EventListener> RecordedListeners { get; } = [];

    public ValueTask<IReadModel> GetReadModel([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type tReadModel, object? key, long? at = null)
    {
        if (at.HasValue) throw new AtNotAllowedInSelectorException();
        return impl.GetReadModel(tReadModel, key, at);
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel>(long? at = null) where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        if (at.HasValue) throw new AtNotAllowedInSelectorException();
        return Record(impl.GetReadModel<TReadModel>(at));
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key, long? at = null) where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        if (at.HasValue) throw new AtNotAllowedInSelectorException();
        return Record(impl.GetReadModel<TReadModel, TKey>(key, at));
    }

    public ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel>(long? at = null) where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        if (at.HasValue) throw new AtNotAllowedInSelectorException();
        return Record(impl.UnsafeGetReadModelWithoutAuthorization<TReadModel>(at));
    }

    public ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(TKey key, long? at = null) where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        if (at.HasValue) throw new AtNotAllowedInSelectorException();
        return Record(impl.UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(key, at));
    }

    public ValueTask<IReadOnlyList<TReadModel>> GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null) where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new() where TKey : notnull
    {
        if (at.HasValue) throw new AtNotAllowedInSelectorException();
        return Record(impl.GetMultipleReadModels<TReadModel, TKey>(keys, at));
    }

    public ValueTask<IReadOnlyList<TReadModel>> UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null) where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new() where TKey : notnull
    {
        if (at.HasValue) throw new AtNotAllowedInSelectorException();
        return Record(impl.UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(keys, at));
    }

    public IUnitOfWork WithPrefilledVersion(long? version)
    {
        throw new AtNotAllowedInSelectorException();
    }

    public IUnitOfWork CopyWithVersion(long version)
    {
        throw new AtNotAllowedInSelectorException();
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

    public record EventListenerTypeWithKeyAndVersion([property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type Type, object? Key, long Version)
    {
        public async ValueTask<bool> IsStillUpToDate(IUnitOfWork unitOfWork, long? at = null)
        {
            var readModel = (EventListener)await unitOfWork.GetReadModel(Type, Key, at);

            return readModel.LastAcceptedEvent <= Version;
        }
    }

    public void Dispose()
    {
        impl.Dispose();

        GC.SuppressFinalize(this);
    }
}