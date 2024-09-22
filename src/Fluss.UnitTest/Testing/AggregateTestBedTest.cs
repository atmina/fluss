using Fluss.Aggregates;
using Fluss.Events;
using Fluss.Testing;

namespace Fluss.UnitTest.Testing;

public class AggregateTestBedTest
{
    private readonly AggregateTestBed<TestAggregate, string> _testBed = new();

    [Fact]
    public void WithEvents_ShouldAddEventsToEventRepository()
    {
        var testEvent = new TestEvent("test");
        _testBed.WithEvents(testEvent);

        var aggregateTestProperty = "";
        _testBed.Calling(async uow =>
        {
            var aggregate = await uow.GetAggregate<TestAggregate, string>("test");
            aggregateTestProperty = aggregate.TestProperty;
        });

        Assert.Equal("test", aggregateTestProperty);
    }

    [Fact]
    public void WithEventEnvelopes_ShouldAddEventsToEventRepository()
    {
        _testBed
            .WithEventEnvelopes(new EventEnvelope
            {
                Event = new TestEvent("test"),
                Version = 0,
                At = DateTimeOffset.Now,
                By = null,
            });

        var aggregateTestProperty = "";
        _testBed.Calling(async uow =>
        {
            var aggregate = await uow.GetAggregate<TestAggregate, string>("test");
            aggregateTestProperty = aggregate.TestProperty;
        });

        Assert.Equal("test", aggregateTestProperty);
    }

    [Fact]
    public void Calling_WithUnitOfWork_ShouldExecuteAction()
    {
        bool actionExecuted = false;
        _testBed.Calling(uow =>
        {
            actionExecuted = true;
            return Task.CompletedTask;
        });

        Assert.True(actionExecuted);
    }

    [Fact]
    public void Calling_WithKeyAndAggregate_ShouldExecuteAction()
    {
        string testKey = "testKey";
        bool actionExecuted = false;
        _testBed.Calling(testKey, aggregate =>
        {
            actionExecuted = true;
            Assert.Equal(testKey, aggregate.Id);
            return Task.CompletedTask;
        });

        Assert.True(actionExecuted);
    }

    [Fact]
    public void Ignoring_ShouldIgnoreSpecifiedType()
    {
        _testBed
            .Calling("test", aggregate => aggregate.DoSomethingElse())
            .Ignoring<Guid>()
            .ResultsIn(new TestEventWithIgnoredProperty(default));
    }

    [Fact]
    public void ResultsIn_ShouldAssertCorrectEvents()
    {
        _testBed
            .Calling("test", aggregate => aggregate.DoSomething())
            .ResultsIn(new TestEvent("test"));
    }

    private record TestAggregate : AggregateRoot<string>
    {
        public string TestProperty { get; private init; } = "";

        public async Task DoSomething()
        {
            await Apply(new TestEvent(Id));
        }

        public async Task DoSomethingElse()
        {
            await Apply(new TestEventWithIgnoredProperty(Guid.NewGuid()));
        }

        protected override AggregateRoot When(EventEnvelope envelope)
        {
            return envelope.Event switch
            {
                TestEvent testEvent => this with { TestProperty = TestProperty + testEvent.TestProperty },
                _ => this
            };
        }
    }

    private record TestEvent(string TestProperty) : Event;

    private record TestEventWithIgnoredProperty(Guid IgnoredProperty) : Event;
}