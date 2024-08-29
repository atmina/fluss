using System.Reflection;
using Fluss.Aggregates;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.Validation;
using Moq;
using Xunit;

namespace Fluss.Testing;

public class AggregateTestBed<TAggregate, TKey> : EventTestBed where TAggregate : AggregateRoot<TKey>, new()
{
    private readonly UnitOfWork _unitOfWork;
    private readonly List<Type> _ignoredTypes = [];

    public AggregateTestBed()
    {
        var validator = new Mock<IRootValidator>();
        validator.Setup(v => v.ValidateEvent(It.IsAny<EventEnvelope>(), It.IsAny<IReadOnlyList<EventEnvelope>?>()))
            .Returns<EventEnvelope, IReadOnlyList<EventEnvelope>?>((_, _) => Task.CompletedTask);
        validator.Setup(v => v.ValidateAggregate(It.IsAny<AggregateRoot>(), It.IsAny<UnitOfWork>()))
            .Returns<AggregateRoot, UnitOfWork>((_, _) => Task.CompletedTask);

        _unitOfWork = new UnitOfWork(EventRepository, EventListenerFactory, [new AllowAllPolicy()],
            new UserIdProvider(_ => Guid.Empty, null!), validator.Object);
    }

    public AggregateTestBed<TAggregate, TKey> Calling(Func<UnitOfWork, Task> action)
    {
        action(_unitOfWork).GetAwaiter().GetResult();
        return this;
    }

    public AggregateTestBed<TAggregate, TKey> Calling(TKey key, Func<TAggregate, Task> action)
    {
        var aggregate = _unitOfWork.GetAggregate<TAggregate, TKey>(key).AsTask().Result;
        action(aggregate).GetAwaiter().GetResult();
        return this;
    }

    public AggregateTestBed<TAggregate, TKey> Ignoring<TIgnoreType>()
    {
        _ignoredTypes.Add(typeof(TIgnoreType));

        return this;
    }

    public void ResultsIn(params Event[] expectedEvents)
    {
        var publishedEvents = _unitOfWork.PublishedEventEnvelopes.Select(ee => ee.Event).ToArray();

        if (expectedEvents.Length == publishedEvents.Length)
        {
            for (var i = 0; i < expectedEvents.Length; i++)
            {
                expectedEvents[i] = GetEventRespectingIgnoredTypes(expectedEvents[i], publishedEvents[i]);
            }
        }

        Assert.Equal(expectedEvents, publishedEvents);
    }

    private Event GetEventRespectingIgnoredTypes(Event expected, Event published)
    {
        if (expected.GetType() != published.GetType())
        {
            return expected;
        }

        var cloneMethod = expected.GetType().GetMethod("<Clone>$");
        var exp = (Event)cloneMethod!.Invoke(expected, [])!;
        foreach (var fieldInfo in expected.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (_ignoredTypes.Contains(fieldInfo.FieldType))
            {
                fieldInfo.SetValue(exp, fieldInfo.GetValue(published));
            }
        }

        return exp;

    }

    public override AggregateTestBed<TAggregate, TKey> WithEvents(params Event[] events)
    {
        base.WithEvents(events);
        return this;
    }

    public override AggregateTestBed<TAggregate, TKey> WithEventEnvelopes(params EventEnvelope[] eventEnvelopes)
    {
        base.WithEventEnvelopes(eventEnvelopes);
        return this;
    }

    private class AllowAllPolicy : Policy
    {
        public ValueTask<bool> AuthenticateEvent(EventEnvelope envelope, IAuthContext authContext)
        {
            return ValueTask.FromResult(true);
        }
    }
}
