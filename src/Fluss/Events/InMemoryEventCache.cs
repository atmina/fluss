using System.Collections.ObjectModel;

namespace Fluss.Events;

public class InMemoryEventCache(long cacheSizePerItem = 10_000) : EventRepositoryPipeline
{
    private readonly List<EventEnvelope[]> _cache = [];
    private long _lastKnownVersion = -1;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public override async ValueTask<ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>>> GetEvents(long fromExclusive,
        long toInclusive)
    {
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.EventRequest", $"{fromExclusive}-{toInclusive}");
        activity?.SetTag("EventSourcing.EventRepository", nameof(InMemoryEventCache));

        if (fromExclusive >= toInclusive)
        {
            return Array.Empty<ReadOnlyMemory<EventEnvelope>>().AsReadOnly();
        }

        await EnsureEventsLoaded(toInclusive);

        var fromItemId = GetCacheKey(fromExclusive + 1);
        var toItemId = GetCacheKey(toInclusive);

        if (fromItemId == toItemId)
        {
            return new[] {
                _cache[fromItemId]
                    .AsMemory((int)((fromExclusive + 1) % cacheSizePerItem), (int)(toInclusive - fromExclusive)).AsReadOnly()
            }.AsReadOnly();
        }

        var result = new ReadOnlyMemory<EventEnvelope>[toItemId - fromItemId + 1];

        result[0] = _cache[fromItemId].AsMemory((int)((fromExclusive + 1) % cacheSizePerItem));
        for (var i = fromItemId + 1; i < toItemId; i++)
        {
            result[i - fromItemId] = _cache[i].AsMemory();
        }

        result[^1] = _cache[toItemId].AsMemory(0, (int)(toInclusive % cacheSizePerItem) + 1);

        return result.AsReadOnly();
    }

    private async Task EnsureEventsLoaded(long to)
    {
        if (_lastKnownVersion >= to)
        {
            return;
        }

        await _loadLock.WaitAsync();

        try
        {
            if (_lastKnownVersion >= to)
            {
                return;
            }

            AddEvents(await base.GetEvents(_lastKnownVersion, to));
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public override async ValueTask Publish(IReadOnlyList<EventEnvelope> events)
    {
        using var activity = FlussActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.EventRepository", nameof(InMemoryEventCache));

        await base.Publish(events);

        await _loadLock.WaitAsync();
        try
        {
            AddEvents(events.ToPagedMemory());
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private int GetCacheKey(long i)
    {
        return (int)(i / cacheSizePerItem);
    }

    private long MinItemForCache(int itemId)
    {
        return cacheSizePerItem * itemId;
    }

    private void AddEvents(ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> eventEnvelopes)
    {
        using var activity = FlussActivitySource.Source.StartActivity();

        foreach (var eventEnvelopeMemory in eventEnvelopes)
        {
            var readOnlySpan = eventEnvelopeMemory.Span;

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var index = 0; index < readOnlySpan.Length; index++)
            {
                var eventEnvelope = readOnlySpan[index];
                var cacheKey = GetCacheKey(eventEnvelope.Version);
                while (_cache.Count <= cacheKey)
                {
                    _cache.Add(new EventEnvelope[cacheSizePerItem]);
                }

                _cache[cacheKey][eventEnvelope.Version - MinItemForCache(cacheKey)] = eventEnvelope;

                // AddEvents could be executed out of order, so we only update the lastKnownVersion if we are sure, that all events before are fetched
                if (_lastKnownVersion == eventEnvelope.Version - 1)
                {
                    _lastKnownVersion = eventEnvelope.Version;
                }
            }
        }
    }
}
