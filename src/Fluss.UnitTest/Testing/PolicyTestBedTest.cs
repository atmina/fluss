using System.Security.Cryptography.X509Certificates;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.ReadModel;
using Fluss.Testing;
using Xunit.Sdk;

namespace Fluss.UnitTest.Testing;

public class PolicyTestBedTest
{
    private readonly PolicyTestBed<TestPolicy> _testBed = new();
    private static readonly Guid AllowedUserId = Guid.NewGuid();

    [Fact]
    public void WithUser_ShouldSetUserId()
    {
        _testBed.WithUser(AllowedUserId);

        _testBed.Allows(new OnlyIfUserMatchesEvent());
    }

    [Fact]
    public void WithUser_RefusesForDifferentUser()
    {
        _testBed.WithUser(Guid.NewGuid());

        _testBed.Refuses(new OnlyIfUserMatchesEvent());
    }

    [Fact]
    public void Allows_ShouldPassForAllowedEvents()
    {
        var allowedEvent = new TestEvent();
        _testBed.Allows(allowedEvent);
        // If no exception is thrown, the test passes
    }

    [Fact]
    public void Allows_ShouldThrowForDisallowedEvents()
    {
        var disallowedEvent = new DisallowedTestEvent();
        Assert.ThrowsAny<XunitException>(() => _testBed.Allows(disallowedEvent));
    }

    [Fact]
    public void Refuses_ShouldPassForDisallowedEvents()
    {
        var disallowedEvent = new DisallowedTestEvent();
        _testBed.Refuses(disallowedEvent);
        // If no exception is thrown, the test passes
    }

    [Fact]
    public void Refuses_ShouldThrowForAllowedEvents()
    {
        var allowedEvent = new TestEvent();
        Assert.ThrowsAny<XunitException>(() => _testBed.Refuses(allowedEvent));
    }

    [Fact]
    public void WithEvents_ShouldRefuseOnlyIfNoTestEventWasAdded()
    {
        _testBed.WithEvents(new TestEvent());

        _testBed.Refuses(new OnlyIfNoTestEventWasAdded());
    }

    [Fact]
    public void WithEventEnvelopes_ShouldRefuseOnlyIfNoTestEventWasAdded()
    {
        _testBed.WithEventEnvelopes(new EventEnvelope
        {
            Event = new TestEvent(),
            Version = 0,
            At = DateTimeOffset.Now,
            By = null,
        });

        _testBed.Refuses(new OnlyIfNoTestEventWasAdded());
    }

    [Fact]
    public void WithEventEnvelopes_ShouldAddEnvelopesToRepository()
    {
        var envelopes = new[]
        {
            new EventEnvelope { Event = new TestEvent(), Version = 0 },
            new EventEnvelope { Event = new TestEvent(), Version = 1 }
        };
        _testBed.WithEventEnvelopes(envelopes);

        _testBed.Allows(new TestEvent());
    }

    private class TestPolicy : Policy
    {
        public async ValueTask<bool> AuthenticateEvent(EventEnvelope envelope, IAuthContext authContext)
        {
            return envelope.Event switch
            {
                TestEvent => true,
                OnlyIfUserMatchesEvent => authContext.UserId == AllowedUserId,
                OnlyIfNoTestEventWasAdded => await authContext.CacheAndGet("test", async () =>
                {
                    var readModel = await authContext.GetReadModel<SawTestEvent>();
                    return !readModel.Saw;
                }),
                _ => false
            };
        }
    }

    private record TestEvent : Event;
    private record OnlyIfUserMatchesEvent : Event;
    private record OnlyIfNoTestEventWasAdded : Event;
    private record DisallowedTestEvent : Event;

    private record SawTestEvent : RootReadModel
    {
        public bool Saw { get; private set; }
        protected override EventListener When(EventEnvelope envelope)
        {
            return envelope.Event switch
            {
                TestEvent => this with { Saw = true },
                _ => this,
            };
        }
    }
}