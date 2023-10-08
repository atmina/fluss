using Fluss.Events;
using Microsoft.Extensions.Logging;

namespace Fluss.Upcasting;

public interface AwaitableService
{
    public Task WaitForCompletionAsync();
}

public class EventUpcasterService : AwaitableService
{
    private List<IUpcaster> _sortedUpcasters;
    private IEventRepository _eventRepository;
    private ILogger<EventUpcasterService> _logger;

    private CancellationTokenSource _onCompletedSource;

    public EventUpcasterService(IEnumerable<IUpcaster> upcasters, UpcasterSorter sorter, IEventRepository eventRepository, ILogger<EventUpcasterService> logger)
    {
        _sortedUpcasters = sorter.SortByDependencies(upcasters);
        _eventRepository = eventRepository;
        _logger = logger;

        _onCompletedSource = new CancellationTokenSource();
    }

    public async ValueTask Run()
    {
        var events = await _eventRepository.GetRawEvents();

        foreach (var upcaster in _sortedUpcasters)
        {
            _logger.LogInformation("Running Upcaster {UpcasterName}", upcaster.GetType().Name);

            var upcastedEvents = new List<RawEventEnvelope>();

            foreach (var @event in events)
            {
                var upcastResult = upcaster.Upcast(@event.RawEvent);
                if (upcastResult is null)
                {
                    upcastedEvents.Add(@event);
                    continue;
                }

                var envelopes = upcastResult.Select((json, i) => new RawEventEnvelope { RawEvent = json, At = @event.At, By = @event.By, Version = upcastedEvents.Count + i }).ToList();
                await _eventRepository.ReplaceEvent(upcastedEvents.Count, envelopes);

                upcastedEvents.AddRange(envelopes);
            }

            events = upcastedEvents;
        }

        _onCompletedSource.Cancel();
    }

    public async Task WaitForCompletionAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        _onCompletedSource.Token.Register(s => ((TaskCompletionSource<bool>)s!).SetResult(true), tcs);
        await tcs.Task;
    }
}
