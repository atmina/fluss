using System.Collections.ObjectModel;
using Collections.Pooled;

namespace Fluss.Events;

public static class EventMemoryArrayExtensions
{
    private static readonly ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> EmptyPagedMemory = new([]);
    public static ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> ToPagedMemory(this IReadOnlyList<EventEnvelope> envelopes)
    {
        if (envelopes.Count == 0)
        {
            return EmptyPagedMemory.AsReadOnly();
        }

        return new[] {
            envelopes.ToArray().AsMemory().AsReadOnly()
        }.AsReadOnly();
    }

    public static PooledList<EventEnvelope> ToFlatEventList(this ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> pagedMemory)
    {
        var result = new PooledList<EventEnvelope>();

        foreach (var memory in pagedMemory)
        {
            result.AddRange(memory.ToArray());
        }

        return result;
    }

    public static async ValueTask<PooledList<EventEnvelope>> ToFlatEventList(this ValueTask<ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>>> pagedMemory)
    {
        return (await pagedMemory).ToFlatEventList();
    }

    public static ReadOnlyMemory<T> AsReadOnly<T>(this Memory<T> memory)
    {
        return memory;
    }
}
