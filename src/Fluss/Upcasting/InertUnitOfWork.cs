using System.Collections.Concurrent;
using Fluss.Aggregates;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss.Upcasting;

/**
 * A custom implementation of the IUnitOfWork interface aimed at providing very basic EventSourcing functionality for use in Upcasters.
 * Neither the envelope's At value, nor the By value can be trusted!
 */
public class InertUnitOfWork : IWriteUnitOfWork
{
    public IReadOnlyCollection<EventListener> ReadModels { get; } = new List<EventListener>();
    private readonly IEventRepository _eventRepository = new InMemoryEventCache
    {
        Next = new InMemoryEventRepository()
    };
    private readonly IEventListenerFactory _eventListenerFactory;

    public InertUnitOfWork()
    {
        _eventListenerFactory = new InMemoryEventListenerCache
        {
            Next = new EventListenerFactory(_eventRepository)
        };
    }

    public ValueTask<TAggregate> GetAggregate<TAggregate, TKey>(TKey key) where TAggregate : AggregateRoot<TKey>, new()
    {
        throw new Exception($"Aggregates are by design not functional on the {nameof(InertUnitOfWork)}");
    }

    public ValueTask<TAggregate> GetAggregate<TAggregate>() where TAggregate : AggregateRoot, new()
    {
        throw new Exception($"Aggregates are by design not functional on the {nameof(InertUnitOfWork)}");
    }

    public async ValueTask Publish(Event @event, AggregateRoot? aggregate = null)
    {
        var currentVersion = await ConsistentVersion();
        await _eventRepository.Publish(new[] {
            new EventEnvelope {
                Event = @event,
                At = DateTimeOffset.Now,
                Version = currentVersion + 1,
                By = new Guid(),
            }
        });
    }

    public async ValueTask<long> ConsistentVersion()
    {
        return await _eventRepository.GetLatestVersion();
    }

    public async ValueTask<IReadModel> GetReadModel(Type tReadModel, object? key, long? at = null)
    {
        if (Activator.CreateInstance(tReadModel) is not EventListener eventListener)
        {
            throw new InvalidOperationException("Type " + tReadModel.FullName + " is not a event listener.");
        }

        if (eventListener is not IReadModel readModel)
        {
            throw new InvalidOperationException("Type " + tReadModel.FullName + " is not a read model.");
        }

        if (eventListener is IEventListenerWithKey eventListenerWithKey)
        {
            typeof(IEventListenerWithKey).GetProperty("Id")?.SetValue(eventListenerWithKey, key);
        }

        eventListener = await ApplyEvents(eventListener);
        if (eventListener is not IReadModel newReadModel)
        {
            throw new InvalidOperationException("Type " + tReadModel.FullName + " is not a read model.");
        }

        return newReadModel;
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel>(long? at = null) where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        return UnsafeGetReadModelWithoutAuthorization<TReadModel>(at);
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key, long? at = null) where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        return UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(key, at);
    }

    public async ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel>(long? at = null) where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        var model = new TReadModel();
        model = await ApplyEvents(model, at);

        return model;
    }

    public async ValueTask<TReadModel> UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(TKey key, long? at = null) where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        var model = new TReadModel { Id = key };
        model = await ApplyEvents(model, at);

        return model;
    }

    public ValueTask<IReadOnlyList<TReadModel>> GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null) where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new() where TKey : notnull
    {
        return UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(keys, at);
    }

    public async ValueTask<IReadOnlyList<TReadModel>>
        UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(IEnumerable<TKey> keys, long? at = null)
        where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new() where TKey : notnull
    {
        var dictionary = new ConcurrentDictionary<TKey, TReadModel>();
        var keysList = keys.ToList();

        await Parallel.ForEachAsync(keysList, async (key, _) =>
        {
            dictionary[key] = await UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(key, at);
        });

        return keysList.Select(k => dictionary[k]).ToList();
    }

    public IUnitOfWork WithPrefilledVersion(long? version)
    {
        throw new Exception($"WithPrefilledVersion is by design not functional on the {nameof(InertUnitOfWork)}");
    }

    public ValueTask Return()
    {
        return ValueTask.CompletedTask;
    }

    private async Task<TEventListener> ApplyEvents<TEventListener>(TEventListener readModel, long? at = null) where TEventListener : EventListener
    {
        return await _eventListenerFactory.UpdateTo(readModel, at ?? await ConsistentVersion());
    }
}
