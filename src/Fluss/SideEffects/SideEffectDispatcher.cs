using System.Reflection;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.Events.TransientEvents;
using Fluss.Upcasting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluss.SideEffects;

public sealed class SideEffectDispatcher : IHostedService
{
    private readonly Dictionary<Type, List<(SideEffect effect, MethodInfo handler)>> _sideEffectCache = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly TransientEventAwareEventRepository _transientEventRepository;

    private long _persistedVersion = -1;
    private long _transientVersion = -1;

    private readonly SemaphoreSlim _dispatchLock = new(1, 1);
    private readonly ILogger<SideEffectDispatcher> _logger;

    public SideEffectDispatcher(IEnumerable<SideEffect> sideEffects, IServiceProvider serviceProvider,
        TransientEventAwareEventRepository transientEventRepository, ILogger<SideEffectDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _transientEventRepository = transientEventRepository;
        _logger = logger;

        CacheSideEffects(sideEffects);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Init();
        return Task.CompletedTask;
    }

    private async void Init()
    {
        var upcaster = _serviceProvider.GetRequiredService<EventUpcasterService>();
        await upcaster.WaitForCompletionAsync();
        _transientEventRepository.NewEvents += HandleNewEvents;
        _transientEventRepository.NewTransientEvents += HandleNewTransientEvents;
    }

    private async void HandleNewEvents(object? sender, EventArgs eventArgs)
    {
        await _dispatchLock.WaitAsync();

        var latestVersion = await _transientEventRepository.GetLatestVersion();
        using var newEvents = await _transientEventRepository.GetEvents(_persistedVersion, latestVersion).ToFlatEventList();

        try
        {
            await DispatchSideEffects(newEvents);
        }
        finally
        {
            _dispatchLock.Release();
        }

        _persistedVersion = latestVersion;
    }

    private async void HandleNewTransientEvents(object? sender, EventArgs eventArgs)
    {
        // We want to fetch the events before hitting the lock to avoid missing events.
        using var currentEvents = _transientEventRepository.GetCurrentTransientEvents().ToFlatEventList();
        await _dispatchLock.WaitAsync();

        var newEvents = currentEvents.Where(e => e.Version > _transientVersion);

        try
        {
            await DispatchSideEffects(newEvents);
            if (currentEvents.Any())
            {
                _transientVersion = currentEvents.Max(e => e.Version);
            }
        }
        finally
        {
            _dispatchLock.Release();
        }
    }

    private void CacheSideEffects(IEnumerable<SideEffect> sideEffects)
    {
        foreach (var sideEffect in sideEffects)
        {
            var eventType = sideEffect.GetType().GetInterface(typeof(SideEffect<>).Name)!.GetGenericArguments()[0];
            if (!_sideEffectCache.TryGetValue(eventType, out var value))
            {
                value = [];
                _sideEffectCache[eventType] = value;
            }

            var method = sideEffect.GetType().GetMethod(nameof(SideEffect<Event>.HandleAsync))!;
            value.Add((sideEffect, method));
        }
    }

    private async Task DispatchSideEffects(IEnumerable<EventEnvelope> events)
    {
        var eventList = events.Where(e => _sideEffectCache.ContainsKey(e.Event.GetType())).ToList();

        while (eventList.Count != 0)
        {
            var userId = eventList.First().By;
            var userEvents = eventList.TakeWhile(e => e.By == userId).ToList();
            var unitOfWorkFactory = _serviceProvider.GetUserUnitOfWorkFactory(userId ?? SystemUser.SystemUserGuid);

            foreach (var envelope in userEvents)
            {
                var type = envelope.Event.GetType();
                var sideEffects = _sideEffectCache[type];

                foreach (var (sideEffect, handleAsync) in sideEffects)
                {
                    try
                    {
                        await unitOfWorkFactory.Commit(async unitOfWork =>
                        {
                            // For transient events we use the most recent persisted version
                            long? version = envelope.Event is not TransientEvent ? envelope.Version : null;
                            var versionedUnitOfWork = unitOfWork.WithPrefilledVersion(version);

                            var invocationResult = handleAsync.Invoke(sideEffect, [envelope.Event, versionedUnitOfWork]);
                            if (invocationResult is not Task<IEnumerable<Event>> resultTask)
                            {
                                throw new Exception(
                                    $"Result of SideEffect {sideEffect.GetType().Name} handler is not a Task<IEnumerable<Event>>");
                            }

                            var newEvents = await resultTask;
                            foreach (var newEvent in newEvents)
                            {
                                await unitOfWork.Publish(newEvent);
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "");
                    }
                }
            }

            eventList = eventList.Except(userEvents).ToList();
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        _transientEventRepository.NewEvents -= HandleNewEvents;
        _transientEventRepository.NewTransientEvents -= HandleNewTransientEvents;

        return Task.CompletedTask;
    }
}
