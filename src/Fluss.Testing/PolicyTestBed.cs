using Fluss.Authentication;
using Fluss.Events;
using Fluss.Extensions;
using Fluss.ReadModel;
using Xunit;

namespace Fluss.Testing;

public class PolicyTestBed<TPolicy> : EventTestBed where TPolicy : Policy, new()
{
    private Guid _userId = Guid.Empty;
    private readonly TPolicy _policy;
    private readonly AuthContextMock _authContext;

    public PolicyTestBed()
    {
        _policy = new TPolicy();
        _authContext = new AuthContextMock(this);
    }

    public PolicyTestBed<TPolicy> WithUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public PolicyTestBed<TPolicy> Allows(params Event[] events)
    {
        foreach (var @event in events)
        {
            Assert.True(_policy.AuthenticateEvent(GetEnvelope(@event), _authContext).GetResult(), $"Event should be allowed {@event}");
        }

        AssertPolicyDoesNoAllowCanary();

        return this;
    }

    public PolicyTestBed<TPolicy> Refuses(params Event[] events)
    {
        foreach (var @event in events)
        {
            Assert.False(_policy.AuthenticateEvent(GetEnvelope(@event), _authContext).GetResult(), $"Event should not be allowed {@event}");
        }

        AssertPolicyDoesNoAllowCanary();

        return this;
    }

    private void AssertPolicyDoesNoAllowCanary()
    {
        Assert.False(_policy.AuthenticateEvent(new EventEnvelope
        {
            At = DateTimeOffset.Now,
            Event = new CanaryEvent(),
            Version = EventRepository.GetLatestVersion().GetResult() + 1
        }, _authContext).GetResult(), "Policy should not allow any event");
    }

    public override PolicyTestBed<TPolicy> WithEvents(params Event[] events)
    {
        base.WithEvents(events);
        return this;
    }

    public override PolicyTestBed<TPolicy> WithEventEnvelopes(params EventEnvelope[] eventEnvelopes)
    {
        base.WithEventEnvelopes(eventEnvelopes);
        return this;
    }

    private EventEnvelope GetEnvelope(Event @event)
    {
        return new EventEnvelope
        {
            At = DateTimeOffset.Now,
            By = _userId,
            Event = @event,
            Version = EventRepository.GetLatestVersion().GetResult()
        };
    }

    private class AuthContextMock : IAuthContext
    {
        private readonly PolicyTestBed<TPolicy> _policyTestBed;

        public AuthContextMock(PolicyTestBed<TPolicy> policyTestBed)
        {
            _policyTestBed = policyTestBed;
        }

        public async ValueTask<T> CacheAndGet<T>(string key, Func<Task<T>> func)
        {
            return await func();
        }

        public async ValueTask<TReadModel> GetReadModel<TReadModel>() where TReadModel : EventListener, IRootEventListener, IReadModel, new()
        {
            return await _policyTestBed.EventListenerFactory.UpdateTo(new TReadModel(), await _policyTestBed.EventRepository.GetLatestVersion());
        }

        public async ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key) where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
        {
            return await _policyTestBed.EventListenerFactory.UpdateTo(new TReadModel { Id = key }, await _policyTestBed.EventRepository.GetLatestVersion());
        }

        public async ValueTask<IReadOnlyList<TReadModel>> GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys) where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new() where TKey : notnull
        {
            return await Task.WhenAll(keys.Select(async k => await GetReadModel<TReadModel, TKey>(k)));
        }

        public Guid UserId => _policyTestBed._userId;
    }
}
