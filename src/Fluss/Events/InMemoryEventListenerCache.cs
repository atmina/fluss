using Microsoft.Extensions.Caching.Memory;

namespace Fluss.Events;

public class InMemoryEventListenerCache : EventListenerFactoryPipeline
{
    private MemoryCache _cache = new(new MemoryCacheOptions());

    public override async ValueTask<TEventListener> UpdateTo<TEventListener>(TEventListener eventListener, long to)
    {
        var cached = Retrieve(eventListener, to);

        if (cached.Tag.LastSeen == to)
        {
            return cached;
        }

        var newEventListener = await Next.UpdateTo(cached, to);
        if (!newEventListener.Tag.HasTaint())
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
        if (cachedEntry.Tag.LastAccepted > before)
        {
            return eventListener;
        }

        return CloneTag(cachedEntry);
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
        if (newEventListener.Tag.LastSeen <= cachedEntry.Tag.LastSeen)
        {
            return;
        }

        _cache.Set(key, CloneTag(newEventListener), new MemoryCacheEntryOptions { Size = 1 });
    }

    private TEventListener CloneTag<TEventListener>(TEventListener eventListener)
        where TEventListener : EventListener
    {
        return eventListener with { Tag = eventListener.Tag with { } };
    }

    private object GetKey(EventListener eventListener)
    {
        if (eventListener.GetType().GetInterfaces().Any(x =>
                x.IsGenericType &&
                x.GetGenericTypeDefinition() == typeof(IEventListenerWithKey<>)))
        {
            return (eventListener.GetType(), eventListener.GetType().GetProperty("Id")?.GetValue(eventListener));
        }

        return eventListener.GetType();
    }

    public void Clean()
    {
        _cache.Dispose();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }
}
