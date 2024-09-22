using Fluss.Events;
using Fluss.ReadModel;
using Fluss.Testing;

namespace Fluss.UnitTest.Testing;

public class ReadModelTestBedTest
{
    private readonly ReadModelTestBed _testBed = new();

    [Fact]
    public void ResultsIn_WithRootReadModel_ShouldAssertCorrectReadModel()
    {
        var expectedReadModel = new TestRootReadModel { Value = "test" };
        _testBed
            .WithEvents(new TestEvent("test"))
            .ResultsIn(expectedReadModel);
    }

    [Fact]
    public void ResultsIn_WithReadModelWithKey_ShouldAssertCorrectReadModel()
    {
        var expectedReadModel = new TestReadModelWithKey { Id = "testKey", Value = "test" };
        _testBed
            .WithEvents(new TestEventWithKey("testKey", "test"))
            .ResultsIn<TestReadModelWithKey, string>(expectedReadModel);
    }

    [Fact]
    public void WithEvents_ShouldAddEventsToRepository()
    {
        var testEvent = new TestEvent("test");
        _testBed.WithEvents(testEvent);

        _testBed.ResultsIn(new TestRootReadModel { Value = "test" });
    }

    [Fact]
    public void WithEventEnvelopes_ShouldAddEnvelopesToRepository()
    {
        var envelope = new EventEnvelope
        {
            Event = new TestEvent("test"),
            Version = 0,
            At = DateTimeOffset.Now,
            By = null
        };
        _testBed.WithEventEnvelopes(envelope);

        _testBed.ResultsIn(new TestRootReadModel { Value = "test" });
    }

    private record TestEvent(string Value) : Event;
    private record TestEventWithKey(string Key, string Value) : Event;

    private record TestRootReadModel : RootReadModel
    {
        public string Value { get; set; } = "";

        protected override EventListener When(EventEnvelope envelope)
        {
            return envelope.Event switch
            {
                TestEvent testEvent => this with { Value = testEvent.Value },
                _ => this
            };
        }
    }

    private record TestReadModelWithKey : ReadModelWithKey<string>
    {
        public string Value { get; set; } = "";

        protected override EventListener When(EventEnvelope envelope)
        {
            return envelope.Event switch
            {
                TestEventWithKey testEvent when testEvent.Key == Id => this with { Value = testEvent.Value },
                _ => this
            };
        }
    }
}