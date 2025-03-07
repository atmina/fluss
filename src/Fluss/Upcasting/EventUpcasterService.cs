using Fluss.Events;
using Microsoft.Extensions.Logging;

namespace Fluss.Upcasting;

public interface AwaitableService
{
    public Task WaitForCompletionAsync();
}

public class EventUpcasterService(
    IEnumerable<Upcaster> upcasters,
    UpcasterSorter sorter,
    IEventRepository eventRepository,
    ILogger<EventUpcasterService> logger)
    : AwaitableService
{
    private readonly List<Upcaster> _sortedUpcasters = sorter.SortByDependencies(upcasters);

    private readonly TaskCompletionSource _onCompletedSource = new();

    public async ValueTask Run()
    {
        var events = (await eventRepository.GetRawEvents()).ToList();

        foreach (var upcaster in _sortedUpcasters)
        {
            logger.LogInformation("Running Upcaster {UpcasterName}", upcaster.GetType().Name);

            var upcastedEvents = new List<RawEventEnvelope>();
            var rawEvents = events.Select(e => e.RawEvent).ToList();

            for (var i = 0; i < rawEvents.Count; i++)
            {
                var @event = events[i];
                var upcastResult = upcaster.Upcast(@event, rawEvents.Skip(i));
                if (upcastResult is null)
                {
                    upcastedEvents.Add(@event);
                    continue;
                }

                var envelopes = upcastResult.Select((json, i) => new RawEventEnvelope { RawEvent = json, At = @event.At, By = @event.By, Version = upcastedEvents.Count + i }).ToList();
                await eventRepository.ReplaceEvent(upcastedEvents.Count, envelopes);

                upcastedEvents.AddRange(envelopes);
            }

            events = upcastedEvents;
        }

        _onCompletedSource.SetResult();
    }

    public async Task WaitForCompletionAsync()
    {
        await _onCompletedSource.Task;
    }
}
