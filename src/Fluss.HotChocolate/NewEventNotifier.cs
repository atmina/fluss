using Fluss.Events;

namespace Fluss.HotChocolate;

public class NewEventNotifier
{
    private long _knownVersion;
    private readonly List<(long startedAtVersion, SemaphoreSlim semaphoreSlim)> _listeners = [];
    private readonly SemaphoreSlim _newEventAvailable = new(0);

    public NewEventNotifier(IBaseEventRepository eventRepository)
    {
        _knownVersion = eventRepository.GetLatestVersion().AsTask().Result;
        eventRepository.NewEvents += EventRepositoryOnNewEvents;

        _ = Task.Run(async () =>
        {
            while (true)
            {
                await _newEventAvailable.WaitAsync();
                var newVersion = await eventRepository.GetLatestVersion();
                if (newVersion <= _knownVersion)
                {
                    continue;
                }

                lock (this)
                {
                    _knownVersion = newVersion;

                    for (var i = _listeners.Count - 1; i >= 0; i--)
                    {
                        if (_listeners[i].startedAtVersion >= newVersion)
                        {
                            continue;
                        }

                        _listeners[i].semaphoreSlim.Release();
                        _listeners.RemoveAt(i);
                    }
                }
            }
            // ReSharper disable once FunctionNeverReturns
        });
    }

    private void EventRepositoryOnNewEvents(object? sender, EventArgs e)
    {
        _newEventAvailable.Release();
    }

    public async Task<long> WaitForEventAfter(long startedAtVersion, CancellationToken ct = default)
    {
        if (_knownVersion > startedAtVersion)
        {
            return _knownVersion;
        }

        SemaphoreSlim semaphore;

        lock (this)
        {
            if (_knownVersion > startedAtVersion)
            {
                return _knownVersion;
            }

            semaphore = new SemaphoreSlim(0, 1);
            _listeners.Add((startedAtVersion, semaphore));
        }

        await semaphore.WaitAsync(ct);

        return _knownVersion;
    }
}
