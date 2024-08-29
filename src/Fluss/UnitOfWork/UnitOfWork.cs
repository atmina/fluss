using Fluss.Authentication;
using Fluss.Events;
using Fluss.Validation;

namespace Fluss.UnitOfWork;

public partial class UnitOfWork : IUnitOfWork
{
    private readonly IEventListenerFactory _eventListenerFactory;
    private readonly IEventRepository _eventRepository;
    private readonly IEnumerable<Policy> _policies;
    private readonly IRootValidator _validator;
    private readonly UserIdProvider _userIdProvider;
    private long? _consistentVersion;

    private Task<long>? _latestVersionLoader;

    public UnitOfWork(IEventRepository eventRepository, IEventListenerFactory eventListenerFactory,
        IEnumerable<Policy> policies, UserIdProvider userIdProvider, IRootValidator validator)
    {
        using var activity = FlussActivitySource.Source.StartActivity();

        _eventRepository = eventRepository;
        _eventListenerFactory = eventListenerFactory;
        _policies = policies;
        _userIdProvider = userIdProvider;
        _validator = validator;
    }

    public async ValueTask<long> ConsistentVersion()
    {
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

            _latestVersionLoader ??= Task.Run(async () =>
            {
                var version = await _eventRepository.GetLatestVersion();
                _consistentVersion = version;
                return version;
            });
        }

        return await _latestVersionLoader;
    }

    public IUnitOfWork WithPrefilledVersion(long? version)
    {
        lock (this)
        {
            if (!_consistentVersion.HasValue && _latestVersionLoader == null)
            {
                _consistentVersion = version;
            }
        }

        return this;
    }

    private Guid CurrentUserId()
    {
        return _userIdProvider.Get();
    }
}
