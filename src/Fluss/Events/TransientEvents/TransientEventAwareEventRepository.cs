using System.Collections.ObjectModel;
using System.Reflection;
using Collections.Pooled;

namespace Fluss.Events.TransientEvents;

public sealed class TransientEventAwareEventRepository : EventRepositoryPipeline, IDisposable
{
    private readonly List<TransientEventEnvelope> _transientEvents = [];
    private long _transientEventVersion;
    private readonly Timer _timer;
    private readonly object _lock = new();
    private static readonly TimeSpan CleanInterval = TimeSpan.FromMilliseconds(100);

    public event EventHandler? NewTransientEvents;

    public TransientEventAwareEventRepository()
    {
        _timer = new Timer(CleanEvents, null, CleanInterval, CleanInterval);
    }

    public ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> GetCurrentTransientEvents()
    {
        lock (_lock)
        {
            return _transientEvents.ToPagedMemory();
        }
    }

    public override async ValueTask Publish(IReadOnlyList<EventEnvelope> events)
    {
        if (events.Count == 0) return;

        using var transientEventEnvelopes = new PooledList<EventEnvelope>();

        foreach (var eventEnvelope in events)
        {
            if (eventEnvelope.Event is TransientEvent)
            {
                transientEventEnvelopes.Add(eventEnvelope);
            }
        }

        if (transientEventEnvelopes.Count == 0)
        {
            await base.Publish(events);
            return;
        }

        // Reset version of persisted events to ensure cache functionality using the first Version received as baseline
        // We can safely fall back to -1 here, since the value will not be used as no events are being published
        var firstPersistedVersion = events.Count > 0 ? events[0].Version : -1;

        var regularEventEnvelopes = new List<EventEnvelope>();

        foreach (var eventEnvelope in events)
        {
            if (eventEnvelope.Event is TransientEvent) continue;

            var rightVersion = firstPersistedVersion + regularEventEnvelopes.Count;

            if (eventEnvelope.Version != rightVersion)
            {
                regularEventEnvelopes.Add(eventEnvelope with { Version = rightVersion });
            }
            else
            {
                regularEventEnvelopes.Add(eventEnvelope);
            }
        }

        await base.Publish(regularEventEnvelopes.ToArray());
        PublishTransientEvents(transientEventEnvelopes);
    }

    private void PublishTransientEvents(IEnumerable<EventEnvelope> events)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0) return;

        var now = DateTimeOffset.Now;

        lock (_lock)
        {
            var newEvents = eventList.Select(e =>
            {
                if (e.Event is not TransientEvent transientEvent) return null;

                var expiresAfter = e.Event.GetType().GetCustomAttribute<ExpiresAfterAttribute>()?.Ms ?? 0;
                var expiresAt = now.AddMilliseconds(expiresAfter);

                return new TransientEventEnvelope
                {
                    ExpiresAt = expiresAt,
                    Event = transientEvent,
                    At = e.At,
                    By = e.By,
                    Version = _transientEventVersion++,
                };
            }).OfType<TransientEventEnvelope>().ToList();

            _transientEvents.AddRange(newEvents);

            NewTransientEvents?.Invoke(this, EventArgs.Empty);
        }
    }

    // This is supposed to act as a 100ms debounce for cleaning the events. Only if the currently active token is not
    //   cancelled after 100ms will the transient events be cleared. This does not invalidate expired events as close
    //   to their expiration time as possible however this is the lesser worry compared to not all listeners getting
    //   all events before they are cleaned up.
    private void CleanEvents(object? _)
    {
        var cleanUntil = DateTimeOffset.Now - CleanInterval;

        lock (_lock)
        {
            _transientEvents.RemoveAll(e => e.ExpiresAt < cleanUntil);
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
