﻿using System.Collections.Concurrent;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss;

public class UnitOfWorkRecordingProxy : IUnitOfWork
{
    private readonly IUnitOfWork _impl;

    public UnitOfWorkRecordingProxy(IUnitOfWork impl)
    {
        _impl = impl;
    }

    public ValueTask<long> ConsistentVersion()
    {
        return _impl.ConsistentVersion();
    }

    public IReadOnlyCollection<EventListener> ReadModels => _impl.ReadModels;
    public ConcurrentQueue<EventEnvelope> PublishedEventEnvelopes => _impl.PublishedEventEnvelopes;

    public List<EventListener> RecordedListeners { get; } = new List<EventListener>();

    public ValueTask<IReadModel> GetReadModel(Type tReadModel, object key, long? at = null)
    {
        return _impl.GetReadModel(tReadModel, key, at);
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel>(long? at = null) where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        return Record(_impl.GetReadModel<TReadModel>(at));
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key, long? at = null) where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        return Record(_impl.GetReadModel<TReadModel, TKey>(key, at));
    }

    public ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel>(long? at = null) where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        return Record(_impl.UnsafeGetReadModelWithoutAuthorization<TReadModel>(at));
    }

    public ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(TKey key, long? at = null) where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        return Record(_impl.UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(key, at));
    }

    public ValueTask<IReadOnlyList<TReadModel>> GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null) where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new() where TKey : notnull
    {
        return Record(_impl.GetMultipleReadModels<TReadModel, TKey>(keys, at));
    }

    public ValueTask<IReadOnlyList<TReadModel>> UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null) where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new() where TKey : notnull
    {
        return Record(_impl.UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(keys, at));
    }

    public IUnitOfWork WithPrefilledVersion(long? version)
    {
        return _impl.WithPrefilledVersion(version);
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
                recordedListener.Tag.LastAccepted
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
                return eventListener.Tag.LastAccepted <= Version;
            }

            return false;
        }
    }
}