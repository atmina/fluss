using Fluss.Events;
using Fluss.Events.TransientEvents;
using Moq;

namespace Fluss.UnitTest.Core.TransientEvents;

public class TransientEventAwareEventRepositoryTest
{
    private readonly Mock<IBaseEventRepository> _baseRepository;
    private readonly TransientEventAwareEventRepository _transientRepository;

    public TransientEventAwareEventRepositoryTest()
    {
        _transientRepository = new TransientEventAwareEventRepository();
        _baseRepository = new Mock<IBaseEventRepository>();

        _transientRepository.Next = _baseRepository.Object;
    }

    [Fact]
    public async Task HidesTransientEventsFromRemainingPipeline()
    {
        var mockEvent = new MockEvent();
        var transientMockEvent = new TransientMockEvent();

        var envelopes = Wrap(mockEvent, transientMockEvent);

        await _transientRepository.Publish(envelopes);
        _baseRepository.Verify(repository => repository.Publish(new[] { envelopes.First() }), Times.Once);

        _baseRepository.Reset();

        var reverseEnvelopes = Wrap(transientMockEvent, mockEvent);
        await _transientRepository.Publish(reverseEnvelopes);
        var expectedBasePublish = reverseEnvelopes.Last() with { Version = 0 };
        _baseRepository.Verify(repository => repository.Publish(new[] { expectedBasePublish }), Times.Once);
    }

    [Fact]
    public async Task DoesntCleanEventsBeforeExpiry()
    {
        var envelopes = Wrap(new TransientMockEvent(), new ExpiringTransientMockEvent());
        await _transientRepository.Publish(envelopes);

        var firstResult = _transientRepository.GetCurrentTransientEvents().ToFlatEventList();
        Assert.Equal(2, firstResult.Count);

        Thread.Sleep(300);

        var secondResult = _transientRepository.GetCurrentTransientEvents().ToFlatEventList();
        Assert.Single(secondResult);

        Thread.Sleep(150);

        var thirdResult = _transientRepository.GetCurrentTransientEvents().ToFlatEventList();
        Assert.Empty(thirdResult);
    }

    private List<EventEnvelope> Wrap(params Event[] events)
    {
        return events.Select((e, i) =>
            new EventEnvelope { At = DateTimeOffset.Now, By = null, Version = i, Event = e })
            .ToList();
    }

    private class MockEvent : Event { }

    private class TransientMockEvent : TransientEvent { }

    [ExpiresAfter(200)]
    private class ExpiringTransientMockEvent : TransientEvent { }

    private class EventEnvelopeEqualityComparer : IEqualityComparer<EventEnvelope>
    {
        public bool Equals(EventEnvelope? x, EventEnvelope? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null))
            {
                return false;
            }

            if (ReferenceEquals(y, null))
            {
                return false;
            }

            return x.At == y.At && x.Event == y.Event && x.Version == y.Version;
        }

        public int GetHashCode(EventEnvelope obj)
        {
            return obj.Event.GetHashCode();
        }
    }
}
