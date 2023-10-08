using Fluss.Events;
using Fluss.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Fluss.Testing;

public abstract class EventRepositoryTestBase<T> where T : IBaseEventRepository
{
    private static DateTimeOffset RemoveTicks(DateTimeOffset d)
    {
        return d.AddTicks(-(d.Ticks % TimeSpan.TicksPerSecond));
    }

    protected abstract T Repository { get; set; }

    [Fact]
    public async Task ReturnRightLatestVersion()
    {
        Assert.Equal(-1, await Repository.GetLatestVersion());
        await Repository.Publish(GetMockEnvelopes(0, 0));
        Assert.Equal(0, await Repository.GetLatestVersion());
    }

    [Fact]
    public async Task ReturnsPublishedEvents()
    {
        var envelopes = GetMockEnvelopes(0, 5).ToList();
        await Repository.Publish(envelopes);

        var repositoryEvents = await Repository.GetEvents(-1, 5).ToFlatEventList();
        Assert.Equal(envelopes, repositoryEvents, new AtTickIgnoringEnvelopeCompare());
    }

    [Fact]
    public async Task ReturnsMultiplePublishedEvents()
    {
        var envelopes = GetMockEnvelopes(0, 1).ToList();
        await Repository.Publish(envelopes.Take(1));
        await Repository.Publish(envelopes.Skip(1));

        var gottenEnvelopes = await Repository.GetEvents(-1, 1).ToFlatEventList();

        Assert.Equal(envelopes, gottenEnvelopes, new AtTickIgnoringEnvelopeCompare());
    }

    [Fact]
    public async Task ReturnsPartOfMultiplePublishedEvents()
    {
        var envelopes = GetMockEnvelopes(0, 2).ToList();
        await Repository.Publish(envelopes);

        var gottenEnvelopes = await Repository.GetEvents(0, 1).ToFlatEventList();
        Assert.Equal(envelopes.Skip(1).Take(1), gottenEnvelopes, new AtTickIgnoringEnvelopeCompare());
    }

    [Fact]
    public async Task NotifiesOnNewEvent()
    {
        var didNotify = false;

        Repository.NewEvents += (_, _) =>
        {
            didNotify = true;
        };

        await Repository.Publish(GetMockEnvelopes(0, 1));
        await Task.Delay(10);

        Assert.True(didNotify);
    }

    [Fact]
    public async Task ReturnsRawEvents()
    {
        var envelopes = GetMockEnvelopes(0, 2).ToList();
        await Repository.Publish(envelopes);

        var serializer = new JsonSerializer { TypeNameHandling = TypeNameHandling.All, TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full };

        var rawEnvelopes = (await Repository.GetRawEvents()).ToList();
        Assert.Equal(envelopes.Count, rawEnvelopes.Count);
        for (int i = 0; i < envelopes.Count; i++)
        {
            var envelope = envelopes[i];
            var rawEnvelope = rawEnvelopes[i];

            Assert.Equal(envelope.By, rawEnvelope.By);
            Assert.Equal(envelope.Version, rawEnvelope.Version);
            Assert.Equal(RemoveTicks(envelope.At), RemoveTicks(rawEnvelope.At));
            Assert.Equal(JObject.FromObject(envelope.Event, serializer), rawEnvelope.RawEvent);
        }
    }

    [Fact]
    public async Task ReplacesEvents()
    {
        var envelopes = GetMockEnvelopes(0, 2).ToList();
        await Repository.Publish(envelopes);

        var replacements = GetMockEnvelopes(0, 2).Select(e => new RawEventEnvelope
        {
            At = e.At,
            Version = e.Version + 1,
            By = e.By,
            RawEvent = e.Event.ToJObject()
        });

        await Repository.ReplaceEvent(1, replacements);

        var latestVersion = await Repository.GetLatestVersion();
        Assert.Equal(4, latestVersion);

        var repoEvents = await Repository.GetEvents(-1, latestVersion).ToFlatEventList();

        for (var i = 0; i <= latestVersion; i++)
        {
            Assert.Equal(i, repoEvents[i].Version);
        }
    }

    [Fact]
    public async Task DeletesEvents()
    {
        var envelopes = GetMockEnvelopes(0, 2).ToList();
        await Repository.Publish(envelopes);

        await Repository.ReplaceEvent(1, Enumerable.Empty<RawEventEnvelope>());

        var latestVersion = await Repository.GetLatestVersion();
        Assert.Equal(1, latestVersion);

        var repoEvents = await Repository.GetEvents(-1, latestVersion).ToFlatEventList();

        for (var i = 0; i <= latestVersion; i++)
        {
            Assert.Equal(i, repoEvents[i].Version);
        }
    }

    [Fact]
    public async Task SignalsRetryOnOutOfOrderEvents()
    {
        var envelopes = GetMockEnvelopes(0, 2).ToList();
        var envelopes2 = GetMockEnvelopes(0, 2).ToList();
        await Repository.Publish(envelopes);

        await Assert.ThrowsAsync<RetryException>(async () =>
            await Repository.Publish(envelopes2));
    }

    private IEnumerable<EventEnvelope> GetMockEnvelopes(int from, int to)
    {
        return Enumerable.Range(from, to - from + 1).Select(version =>
                new EventEnvelope { At = DateTimeOffset.UtcNow, By = null, Version = version, Event = new MockEvent() })
            .ToList();
    }

    private record MockEvent : Event;

    public class AtTickIgnoringEnvelopeCompare : EqualityComparer<EventEnvelope>
    {
        public override bool Equals(EventEnvelope? a, EventEnvelope? b)
        {
            if (a is null && b is null)
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return EnvelopeWithAtTickRemoved(a) == EnvelopeWithAtTickRemoved(b);
        }

        public override int GetHashCode(EventEnvelope envelope)
        {
            return EnvelopeWithAtTickRemoved(envelope).GetHashCode();
        }

        private EventEnvelope EnvelopeWithAtTickRemoved(EventEnvelope envelope)
        {
            return envelope with { At = RemoveTicks(envelope.At) };
        }
    }
}
