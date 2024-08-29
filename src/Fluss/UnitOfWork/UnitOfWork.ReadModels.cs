using System.Collections.Concurrent;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss;

public partial class UnitOfWork
{
    private readonly ConcurrentBag<EventListener> _readModels = new();
    public IReadOnlyCollection<EventListener> ReadModels => _readModels;

    public async ValueTask<IReadModel> GetReadModel(Type tReadModel, object? key, long? at = null)
    {
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.ReadModel", tReadModel.FullName);

        if (Activator.CreateInstance(tReadModel) is not EventListener eventListener)
        {
            throw new InvalidOperationException("Type " + tReadModel.FullName + " is not a event listener.");
        }

        if (eventListener is IEventListenerWithKey eventListenerWithKey)
        {
            eventListenerWithKey.GetType().GetProperty("Id")?.SetValue(eventListenerWithKey, key);
        }

        eventListener = await UpdateAndApplyPublished(eventListener, at);

        if (eventListener is not IReadModel readModel)
        {
            throw new InvalidOperationException("Type " + tReadModel.FullName + " is not a read model.");
        }

        if (!await AuthorizeUsage(readModel))
        {
            throw new UnauthorizedAccessException($"Cannot read {eventListener.GetType()} as the current user.");
        }

        if (at is null)
        {
            _readModels.Add(eventListener);
        }

        return readModel;
    }

    public async ValueTask<TReadModel> GetReadModel<TReadModel>(long? at = null)
        where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.ReadModel", typeof(TReadModel).FullName);

        var readModel = await UpdateAndApplyPublished(new TReadModel(), at);

        if (!await AuthorizeUsage(readModel))
        {
            throw new UnauthorizedAccessException($"Cannot read {readModel.GetType()} as the current user.");
        }

        if (at is null)
        {
            _readModels.Add(readModel);
        }

        return readModel;
    }

    public async ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel>(long? at = null)
        where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        var readModel = await UpdateAndApplyPublished(new TReadModel(), at);

        if (at is null)
        {
            _readModels.Add(readModel);
        }

        return readModel;
    }

    public async ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key, long? at = null)
        where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.ReadModel", typeof(TReadModel).FullName);

        var readModel = await UpdateAndApplyPublished(new TReadModel { Id = key }, at);

        if (!await AuthorizeUsage(readModel))
        {
            throw new UnauthorizedAccessException($"Cannot read {readModel.GetType()} as the current user.");
        }

        if (at is null)
        {
            _readModels.Add(readModel);
        }

        return readModel;
    }

    public async ValueTask<TReadModel>
        UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(TKey key, long? at = null)
        where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        var readModel = await UpdateAndApplyPublished(new TReadModel { Id = key }, at);

        if (at is null)
        {
            _readModels.Add(readModel);
        }

        return readModel;
    }

    public async ValueTask<IReadOnlyList<TReadModel>>
        GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null) where TKey : notnull
        where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new()
    {
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.ReadModel", typeof(TReadModel).FullName);

        var dictionary = new ConcurrentDictionary<TKey, TReadModel?>();

        var keysList = keys.ToList();

        await Parallel.ForEachAsync(keysList, async (key, _) =>
        {
            var readModel = await UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(key, at);

            if (await AuthorizeUsage(readModel))
            {
                dictionary[key] = readModel;
            }
            else
            {
                dictionary[key] = null;
            }
        });

        return keysList.Select(k => dictionary[k])
            .Where(readModel => readModel != null)
            .ToList()!;
    }

    public async ValueTask<IReadOnlyList<TReadModel>>
        UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null)
        where TKey : notnull where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new()
    {
        var dictionary = new ConcurrentDictionary<TKey, TReadModel>();

        var keysList = keys.ToList();

        await Parallel.ForEachAsync(keysList, async (key, _) =>
        {
            dictionary[key] = await UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(key, at);
        });

        return keysList.Select(k => dictionary[k]).ToList();
    }
}
