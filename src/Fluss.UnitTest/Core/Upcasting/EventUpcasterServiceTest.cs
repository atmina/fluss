using Fluss.Events;
using Fluss.Upcasting;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;

namespace Fluss.UnitTest.Core.Upcasting;

public class EventUpcasterServiceTest
{
    private static RawEventEnvelope GetRawTestEvent1Envelope(int version)
    {
        var jObject = new TestEvent1("Value").ToJObject();

        return new RawEventEnvelope { Version = version, RawEvent = jObject };
    }

    private static RawEventEnvelope GetRawTestEvent2Envelope(int version)
    {
        var jObject = new TestEvent2("Value2").ToJObject();

        return new RawEventEnvelope { Version = version, RawEvent = jObject };
    }

    private (EventUpcasterService, Mock<IBaseEventRepository>) GetServices(IEnumerable<Upcaster> upcasters, IEnumerable<RawEventEnvelope>? events = null)
    {
        var eventRepository = new Mock<IBaseEventRepository>();
        var logger = new Mock<ILogger<EventUpcasterService>>();

        var usedEvents = events ?? Enumerable.Range(0, 4).Select(GetRawTestEvent1Envelope);
        eventRepository.Setup(repo => repo.GetRawEvents()).Returns(ValueTask.FromResult(usedEvents));

        return (
            new EventUpcasterService(upcasters, new UpcasterSorter(), eventRepository.Object, logger.Object),
            eventRepository
        );
    }

    [Fact]
    public async Task UpcasterReturningNullDoesntReplaceEvents()
    {
        var (upcasterService, eventRepositoryMock) = GetServices([new NoopUpcast()]);
        await upcasterService.Run();

        eventRepositoryMock.Verify(
            repo => repo.ReplaceEvent(
                It.IsAny<long>(),
                It.IsAny<IEnumerable<RawEventEnvelope>>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task SingleEventsAreUpcast()
    {
        var (upcasterService, eventRepositoryMock) = GetServices([new SingleEventUpcast()]);

        await upcasterService.Run();

        eventRepositoryMock.Verify(
            repo => repo.ReplaceEvent(
                It.IsAny<long>(),
                It.Is<IEnumerable<RawEventEnvelope>>(
                    newEvents => newEvents.SingleOrDefault()!.RawEvent["Property1"]!.ToObject<string>() == "Upcast"
                )
            ),
            Times.Exactly(4)
        );
    }

    [Fact]
    public async Task MultipleEventsAreUpcast()
    {
        var (upcasterService, eventRepositoryMock) = GetServices([new MultiEventUpcast()]);

        await upcasterService.Run();

        eventRepositoryMock.Verify(
            repo => repo.ReplaceEvent(
                It.IsAny<long>(),
                It.Is<IEnumerable<RawEventEnvelope>>(newEvents => newEvents.Count() == 3)
            ),
            Times.Exactly(4)
        );
    }

    [Fact]
    public async Task UpcastsAreChainable()
    {
        var events = Enumerable.Range(0, 4)
            .Select(GetRawTestEvent1Envelope)
            .Append(GetRawTestEvent2Envelope(4));

        var (upcasterService, eventRepositoryMock) =
            GetServices([new ChainedEventUpcast2(), new ChainedEventUpcast()], events);

        await upcasterService.Run();

        eventRepositoryMock.Verify(
            repo => repo.ReplaceEvent(
                It.IsAny<long>(),
                It.Is<IEnumerable<RawEventEnvelope>>(
                    newEvents => newEvents.SingleOrDefault()!.RawEvent["Property2"]!.ToObject<string>() == "Value"
                )
            ),
            Times.Exactly(4)
        );

        eventRepositoryMock.Verify(
            repo => repo.ReplaceEvent(
                It.IsAny<long>(),
                It.Is<IEnumerable<RawEventEnvelope>>(
                    newEvents => newEvents.SingleOrDefault()!.RawEvent["Property2"]!.ToObject<string>() == "Upcast-Value"
                )
            ),
            Times.Exactly(4)
        );

        eventRepositoryMock.Verify(
            repo => repo.ReplaceEvent(
                It.IsAny<long>(),
                It.Is<IEnumerable<RawEventEnvelope>>(
                    newEvents => newEvents.SingleOrDefault()!.RawEvent["Property2"]!.ToObject<string>() == "Upcast-Value2")
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task VersionIsSetCorrectly()
    {
        var (upcasterService, eventRepositoryMock) = GetServices([new MultiEventUpcast()], [GetRawTestEvent1Envelope(1)
        ]);

        await upcasterService.Run();

        eventRepositoryMock.Verify(
            repo => repo.ReplaceEvent(
                It.IsAny<long>(),
                It.Is<IEnumerable<RawEventEnvelope>>(
                    newEvents => newEvents.Select((envelope, i) => envelope.Version == i).All(b => b)
                )
            ),
            Times.Once
        );
    }
}

// ReSharper disable once NotAccessedPositionalProperty.Global
record TestEvent1(string Property1) : Event;

// ReSharper disable once NotAccessedPositionalProperty.Global
record TestEvent2(string Property2) : Event;

internal class NoopUpcast : Upcaster
{
    public IEnumerable<JObject>? Upcast(JObject eventJson) => null;
}

internal class SingleEventUpcast : Upcaster
{
    public IEnumerable<JObject>? Upcast(JObject eventJson)
    {
        var type = eventJson.GetValue("$type")?.ToObject<string>();

        if (type != typeof(TestEvent1).AssemblyQualifiedName) return null;

        var clone = (JObject)eventJson.DeepClone();
        clone["Property1"] = "Upcast";

        return [clone];
    }
}

internal class MultiEventUpcast : Upcaster
{
    public IEnumerable<JObject>? Upcast(JObject eventJson)
    {
        var type = eventJson.GetValue("$type")?.ToObject<string>();

        if (type != typeof(TestEvent1).AssemblyQualifiedName) return null;

        return [eventJson, eventJson, eventJson];
    }
}

internal class ChainedEventUpcast : Upcaster
{
    public IEnumerable<JObject>? Upcast(JObject eventJson)
    {
        var type = eventJson.GetValue("$type")?.ToObject<string>();

        if (type != typeof(TestEvent1).AssemblyQualifiedName) return null;

        var clone = (JObject)eventJson.DeepClone();
        clone["Property2"] = clone["Property1"];
        clone.Remove("Property1");
        clone["$type"] = typeof(TestEvent2).AssemblyQualifiedName;

        return [clone];
    }
}

[DependsOn(typeof(ChainedEventUpcast))]
internal class ChainedEventUpcast2 : Upcaster
{
    public IEnumerable<JObject>? Upcast(JObject eventJson)
    {
        var type = eventJson.GetValue("$type")?.ToObject<string>();

        if (type != typeof(TestEvent2).AssemblyQualifiedName) return null;

        var clone = (JObject)eventJson.DeepClone();
        clone["Property2"] = "Upcast-" + clone["Property2"]!.ToObject<string>();

        return [clone];
    }
}
