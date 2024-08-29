using System.Collections.ObjectModel;
using Fluss.Exceptions;
using Newtonsoft.Json;

namespace Fluss.Events;

public class InMemoryEventRepository : IBaseEventRepository
{
    private readonly List<EventEnvelope> _events = [];
    public event EventHandler? NewEvents;

    public ValueTask Publish(IEnumerable<EventEnvelope> eventEnvelopes)
    {
        foreach (var eventEnvelope in eventEnvelopes)
        {
            lock (_events)
            {
                if (eventEnvelope.Version != _events.Count)
                {
                    throw new RetryException();
                }

                _events.Add(eventEnvelope);
            }
        }

        NotifyNewEvents();
        return ValueTask.CompletedTask;
    }

    public ValueTask<ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>>> GetEvents(long fromExclusive, long toInclusive)
    {
        if (toInclusive < fromExclusive)
        {
            throw new InvalidOperationException("From is greater than to");
        }

        return ValueTask.FromResult(
            new[] {
                _events.ToArray().AsMemory((int)fromExclusive + 1, (int)(toInclusive - fromExclusive)).AsReadOnly()
            }.AsReadOnly());
    }

    public ValueTask<IEnumerable<RawEventEnvelope>> GetRawEvents()
    {
        var rawEnvelopes = _events.Select(envelope =>
        {
            var rawEvent = envelope.Event.ToJObject();

            return new RawEventEnvelope
            {
                By = envelope.By,
                Version = envelope.Version,
                At = envelope.At,
                RawEvent = rawEvent,
            };
        });

        return ValueTask.FromResult(rawEnvelopes);
    }

    public ValueTask ReplaceEvent(long at, IEnumerable<RawEventEnvelope> newEvents)
    {
        var checkedAt = checked((int)at);
        var events = newEvents.ToList();

        for (var i = checkedAt; i < _events.Count; i++)
        {
            _events[i] = _events[i] with { Version = _events[i].Version + events.Count - 1 };
        }

        _events.RemoveAt(checkedAt);
        var serializer = new JsonSerializer { TypeNameHandling = TypeNameHandling.All };

        var eventEnvelopes = events.Select(envelope =>
        {
            var @event = envelope.RawEvent.ToObject<Event>(serializer);

            if (@event is null) throw new Exception("Failed to convert raw event to Event");

            return new EventEnvelope { By = envelope.By, At = envelope.At, Version = envelope.Version, Event = @event };
        });

        _events.InsertRange(checkedAt, eventEnvelopes);

        return ValueTask.CompletedTask;
    }

    public ValueTask<long> GetLatestVersion()
    {
        return ValueTask.FromResult<long>(_events.Count - 1);
    }

    private void NotifyNewEvents()
    {
        Task.Run(() =>
        {
            NewEvents?.Invoke(this, EventArgs.Empty);
        });
    }
}
