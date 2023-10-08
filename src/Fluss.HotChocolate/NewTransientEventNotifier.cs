using Fluss.Events.TransientEvents;

namespace Fluss.HotChocolate;

public class NewTransientEventNotifier
{
    private readonly List<(long startedAtVersion, TaskCompletionSource<IEnumerable<TransientEventEnvelope>> task)> _listeners = new();
    private readonly TransientEventAwareEventRepository _transientEventRepository;

    private readonly SemaphoreSlim _newEventAvailable = new(0);

    public NewTransientEventNotifier(TransientEventAwareEventRepository transientEventRepository)
    {
        _transientEventRepository = transientEventRepository;
        transientEventRepository.NewTransientEvents += OnNewTransientEvents;

        _ = Task.Run(async () =>
        {
            while (true)
            {
                await _newEventAvailable.WaitAsync();
                var events = transientEventRepository.GetCurrentTransientEvents();

                lock (this)
                {
                    for (var i = _listeners.Count - 1; i >= 0; i--)
                    {
                        var listener = _listeners[i];
                        var newEvents = new List<TransientEventEnvelope>();
                        foreach (var memory in events)
                        {
                            foreach (var eventEnvelope in memory.ToArray())
                            {
                                if (eventEnvelope.Version <= listener.startedAtVersion)
                                {
                                    continue;
                                }
                                newEvents.Add((TransientEventEnvelope)eventEnvelope);
                            }
                        }

                        // If a listener is re-added before all other ones are handled, there might be a situation where
                        //   there are no new events for that listener; in that case we keep it around
                        if (newEvents.Count == 0) continue;

                        _listeners[i].task.SetResult(newEvents);
                        _listeners.RemoveAt(i);
                    }
                }
            }
        });
    }

    private void OnNewTransientEvents(object? sender, EventArgs e)
    {
        _newEventAvailable.Release();
    }

    public async ValueTask<IEnumerable<TransientEventEnvelope>> WaitForEventAfter(long startedAtVersion, CancellationToken ct = default)
    {
        var events = _transientEventRepository.GetCurrentTransientEvents();

        if (events.LastOrDefault() is { Length: > 0 } lastEventPage && lastEventPage.Span[^1] is { } lastKnown && lastKnown.Version > startedAtVersion)
        {
            var newEvents = new List<TransientEventEnvelope>();
            foreach (var memory in events)
            {
                for (var index = 0; index < memory.Span.Length; index++)
                {
                    var eventEnvelope = memory.Span[index];
                    if (eventEnvelope.Version <= startedAtVersion)
                    {
                        continue;
                    }

                    newEvents.Add((TransientEventEnvelope)eventEnvelope);
                }
            }

            return newEvents;
        }

        TaskCompletionSource<IEnumerable<TransientEventEnvelope>> task;

        lock (this)
        {
            task = new TaskCompletionSource<IEnumerable<TransientEventEnvelope>>();
            _listeners.Add((startedAtVersion, task));
        }

        return await task.Task.WaitAsync(ct);
    }
}
