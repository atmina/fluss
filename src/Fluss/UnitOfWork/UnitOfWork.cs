using System.Diagnostics;
using Collections.Pooled;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.Validation;
using Microsoft.Extensions.ObjectPool;

namespace Fluss;

public partial class UnitOfWork : IWriteUnitOfWork
{
    private class UnitOfWorkObjectPolicy : IPooledObjectPolicy<UnitOfWork>
    {
        public UnitOfWork Create() => new();
        public bool Return(UnitOfWork obj) => true;
    }

    private static readonly ObjectPool<UnitOfWork> Pool = new DefaultObjectPool<UnitOfWork>(new UnitOfWorkObjectPolicy());

    private IEventListenerFactory? _eventListenerFactory;
    private IEventRepository? _eventRepository;
    private readonly PooledList<Policy> _policies = new();
    private IRootValidator? _validator;
    private UserIdProvider? _userIdProvider;
    private long? _consistentVersion;
    private bool _isInstantiated;

    private UnitOfWork()
    {
    }

    public static UnitOfWork Create(IEventRepository eventRepository, IEventListenerFactory eventListenerFactory,
        IEnumerable<Policy> policies, UserIdProvider userIdProvider, IRootValidator validator)
    {
        var unitOfWork = Pool.Get();
        unitOfWork._eventRepository = eventRepository;
        unitOfWork._eventListenerFactory = eventListenerFactory;
        unitOfWork._policies.AddRange(policies);
        unitOfWork._userIdProvider = userIdProvider;
        unitOfWork._validator = validator;
        unitOfWork._isInstantiated = true;
        return unitOfWork;
    }

    private void EnsureInstantiated()
    {
        if (!_isInstantiated)
        {
            throw new InvalidOperationException("UnitOfWork is not properly instantiated.");
        }
    }

    public async ValueTask<long> ConsistentVersion()
    {
        EnsureInstantiated();

        if (_consistentVersion.HasValue)
        {
            return _consistentVersion.Value;
        }

        using var activity = FlussActivitySource.Source.StartActivity();

        lock (this)
        {
            if (_consistentVersion.HasValue)
            {
                return _consistentVersion.Value;
            }
        }

        var version = await _eventRepository!.GetLatestVersion();

        lock (this)
        {
            _consistentVersion ??= version;
        }

        return _consistentVersion.Value;
    }

    public IUnitOfWork WithPrefilledVersion(long? version)
    {
        EnsureInstantiated();

        lock (this)
        {
            _consistentVersion ??= version;
        }

        return this;
    }

    private Guid CurrentUserId()
    {
        EnsureInstantiated();
        return _userIdProvider!.Get();
    }

    public ValueTask Return()
    {
        _eventListenerFactory = null;
        _eventRepository = null;
        _policies.Clear();
        _validator = null;
        _userIdProvider = null;
        _consistentVersion = null;
        
        PublishedEventEnvelopes.Clear();
        _readModels.Clear();
        _isInstantiated = false;

        Pool.Return(this);

        return ValueTask.CompletedTask;
    }
}
