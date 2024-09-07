using Microsoft.Extensions.Caching.Memory;

namespace Fluss.Events;

public class InMemoryEventListenerCache : EventListenerFactoryPipeline
{
    private MemoryCache _cache = new(new MemoryCacheOptions());

    public override async ValueTask<TEventListener> UpdateTo<TEventListener>(TEventListener eventListener, long to)
    {
        var cached = Retrieve(eventListener, to);

        if (cached.LastSeenEvent == to)
        {
            return cached;
        }

        var newEventListener = await Next.UpdateTo(cached, to);
        if (!newEventListener.HasTaint())
        {
            Store(newEventListener);
        }

        return newEventListener;
    }

    private TEventListener Retrieve<TEventListener>(TEventListener eventListener, long before)
        where TEventListener : EventListener
    {

        var key = GetKey(eventListener);
        if (!_cache.TryGetValue(key, out var cached) || cached == null)
        {
            return eventListener;
        }

        var cachedEntry = (TEventListener)cached;
        if (cachedEntry.LastAcceptedEvent > before)
        {
            return eventListener;
        }

        return cachedEntry;
    }

    private void Store<TEventListener>(TEventListener newEventListener) where TEventListener : EventListener
    {
        var key = GetKey(newEventListener);

        if (!_cache.TryGetValue(key, out var cached) || cached == null)
        {
            _cache.Set(key, newEventListener, new MemoryCacheEntryOptions { Size = 1 });
            return;
        }

        var cachedEntry = (TEventListener)cached;
        if (newEventListener.LastSeenEvent <= cachedEntry.LastSeenEvent)
        {
            return;
        }

        _cache.Set(key, newEventListener, new MemoryCacheEntryOptions { Size = 1 });
    }

    private object GetKey(EventListener eventListener)
    {
        if (eventListener is IEventListenerWithKey eventListenerWithKey)
        {
            return (eventListener.GetType(), eventListenerWithKey.Id);
        }

        return eventListener.GetType();
    }

    public void Clean()
    {
        _cache.Dispose();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }
}
