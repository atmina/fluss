using System.Text.Json.Nodes;
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
        var jObject = EventSerializer.Serialize(new TestEvent1("Value"));

        return new RawEventEnvelope { Version = version, RawEvent = jObject };
    }

    private static RawEventEnvelope GetRawTestEvent2Envelope(int version)
    {
        var jObject = EventSerializer.Serialize(new TestEvent2("Value2"));

        return new RawEventEnvelope { Version = version, RawEvent = jObject };
    }

    private (EventUpcasterService, Mock<IBaseEventRepository>) GetServices(IEnumerable<IUpcaster> upcasters, IEnumerable<RawEventEnvelope>? events = null)
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
                    newEvents => newEvents.SingleOrDefault()!.RawEvent["Property1"]!.GetValue<string>() == "Upcast"
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
                    newEvents => newEvents.SingleOrDefault()!.RawEvent["Property2"]!.GetValue<string>() == "Value"
                )
            ),
            Times.Exactly(4)
        );

        eventRepositoryMock.Verify(
            repo => repo.ReplaceEvent(
                It.IsAny<long>(),
                It.Is<IEnumerable<RawEventEnvelope>>(
                    newEvents => newEvents.SingleOrDefault()!.RawEvent["Property2"]!.GetValue<string>() == "Upcast-Value"
                )
            ),
            Times.Exactly(4)
        );

        eventRepositoryMock.Verify(
            repo => repo.ReplaceEvent(
                It.IsAny<long>(),
                It.Is<IEnumerable<RawEventEnvelope>>(
                    newEvents => newEvents.SingleOrDefault()!.RawEvent["Property2"]!.GetValue<string>() == "Upcast-Value2")
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

internal class NoopUpcast : IUpcaster
{
    public IEnumerable<JsonObject>? Upcast(JsonObject eventJson) => null;
}

internal class SingleEventUpcast : IUpcaster
{
    public IEnumerable<JsonObject>? Upcast(JsonObject eventJson)
    {
        var type = eventJson["$type"]!.GetValue<string>();

        if (type != typeof(TestEvent1).AssemblyQualifiedName) return null;

        var clone = new JsonObject(eventJson)
        {
            ["Property1"] = "Upcast"
        };

        return [clone];
    }
}

internal class MultiEventUpcast : IUpcaster
{
    public IEnumerable<JsonObject>? Upcast(JsonObject eventJson)
    {
        var type = eventJson["$type"]!.GetValue<string>();

        if (type != typeof(TestEvent1).AssemblyQualifiedName) return null;

        return [eventJson, eventJson, eventJson];
    }
}

internal class ChainedEventUpcast : IUpcaster
{
    public IEnumerable<JsonObject>? Upcast(JsonObject eventJson)
    {
        var type = eventJson["$type"]!.GetValue<string>();

        if (type != typeof(TestEvent1).AssemblyQualifiedName) return null;

        var clone = new JsonObject(eventJson)
        {
            ["Property2"] = eventJson["Property1"],
            ["$type"] = typeof(TestEvent2).AssemblyQualifiedName
        };
        clone.Remove("Property1");

        return [clone];
    }
}

[DependsOn(typeof(ChainedEventUpcast))]
internal class ChainedEventUpcast2 : IUpcaster
{
    public IEnumerable<JsonObject>? Upcast(JsonObject eventJson)
    {
        var type = eventJson["$type"]!.GetValue<string>();

        if (type != typeof(TestEvent2).AssemblyQualifiedName) return null;

        var clone = new JsonObject(eventJson)
        {
            ["Property2"] = "Upcast-" + eventJson["Property2"]!.GetValue<string>()
        };

        return [clone];
    }
}
