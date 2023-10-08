using System.Collections.ObjectModel;
using System.Reflection;

namespace Fluss.Events.TransientEvents;

public sealed class TransientEventAwareEventRepository : EventRepositoryPipeline
{
    private readonly List<TransientEventEnvelope> _transientEvents = new();
    private long _transientEventVersion;
    private bool _cleanTaskIsRunning = false;
    private bool _anotherCleanTaskRequired = false;

    public event EventHandler? NewTransientEvents;

    public ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> GetCurrentTransientEvents()
    {
        lock (this)
        {
            var result = _transientEvents.ToPagedMemory();
            CleanEvents();
            return result;
        }
    }

    public override async ValueTask Publish(IEnumerable<EventEnvelope> events)
    {
        var eventEnvelopes = events.ToList();
        if (!eventEnvelopes.Any()) return;

        var transientEventEnvelopes = eventEnvelopes.Where(e => e.Event is TransientEvent);

        // Reset version of persisted events to ensure cache functionality using the first Version received as baseline
        // We can safely fall back to -1 here, since the value will not be used as no events are being published
        var firstPersistedVersion = eventEnvelopes.FirstOrDefault()?.Version ?? -1;

        var persistedEventEnvelopes = eventEnvelopes
            .Where(e => e.Event is not TransientEvent)
            .Select((e, i) => e with { Version = firstPersistedVersion + i });

        await base.Publish(persistedEventEnvelopes);
        PublishTransientEvents(transientEventEnvelopes);
    }

    private void PublishTransientEvents(IEnumerable<EventEnvelope> events)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0) return;

        var now = DateTimeOffset.Now;

        lock (this)
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
    private void CleanEvents()
    {
        lock (this)
        {
            if (_cleanTaskIsRunning)
            {
                _anotherCleanTaskRequired = true;
                return;
            }

            // Starting the clean task after the lock
            _cleanTaskIsRunning = true;
        }

        var now = DateTimeOffset.Now;

        // ReSharper disable once AsyncVoidLambda
        new Task(async () =>
        {
            while (true)
            {
                await Task.Delay(100);

                lock (this)
                {
                    if (_anotherCleanTaskRequired)
                    {
                        _anotherCleanTaskRequired = false;
                        now = DateTimeOffset.Now;
                    }
                    else
                    {
                        _transientEvents.RemoveAll(e => e.ExpiresAt < now);
                        _cleanTaskIsRunning = false;
                        return;
                    }
                }
            }
        }).Start();
    }
}
