using System.Collections.ObjectModel;

namespace Fluss.Events;

public static class EventMemoryArrayExtensions
{
    public static ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> ToPagedMemory<T>(this List<T> envelopes) where T : EventEnvelope
    {
        if (envelopes is List<EventEnvelope> casted)
        {
            return new[] {
                casted.ToArray().AsMemory().AsReadOnly()
            }.AsReadOnly();
        }
        else
        {
            return new[] {
                envelopes.Cast<EventEnvelope>().ToArray().AsMemory().AsReadOnly()
            }.AsReadOnly();
        }
    }

    public static IReadOnlyList<EventEnvelope> ToFlatEventList(this ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> pagedMemory)
    {
        var result = new List<EventEnvelope>();

        foreach (var memory in pagedMemory)
        {
            result.AddRange(memory.ToArray());
        }

        return result;
    }

    public static async ValueTask<IReadOnlyList<EventEnvelope>> ToFlatEventList(this ValueTask<ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>>> pagedMemory)
    {
        return (await pagedMemory).ToFlatEventList();
    }

    public static ReadOnlyMemory<T> AsReadOnly<T>(this Memory<T> memory)
    {
        return memory;
    }
}
