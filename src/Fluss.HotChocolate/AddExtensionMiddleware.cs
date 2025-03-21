using System.Diagnostics.Metrics;
using Fluss.Events;
using Fluss.Metrics;
using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RequestDelegate = HotChocolate.Execution.RequestDelegate;

namespace Fluss.HotChocolate;

public class AddExtensionMiddleware(
    RequestDelegate next,
    IServiceProvider rootServiceProvider,
    ILogger<AddExtensionMiddleware> logger)
{
    private const string SubsequentRequestMarker = nameof(AddExtensionMiddleware) + ".subsequentRequestMarker";
    internal static readonly UpDownCounter<int> ActiveLiveQueries = FlussMetrics.Meter.CreateUpDownCounter<int>(
        "active_live_queries",
        unit: "Queries",
        description: "Number of active Live Queries stuck in a while loop."
    );

    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    public async ValueTask InvokeAsync(IRequestContext context)
    {
        await _next.Invoke(context);

        if (!context.ContextData.TryGetValue(nameof(IUnitOfWork), out var unitOfWork))
        {
            return;
        }

        var httpContext = context.Services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var isWebsocket = true == httpContext?.WebSockets.IsWebSocketRequest;
        var isSse = httpContext?.Request.Headers.Accept.ToString() == "text/event-stream";
        if (!isWebsocket && !isSse)
        {
            return;
        }

        if (context.Request.Extensions?.ContainsKey(SubsequentRequestMarker) ?? false)
        {
            if (context.Result is QueryResult subsequentQueryResult)
            {
                context.Result = QueryResultBuilder.FromResult(subsequentQueryResult).AddContextData(nameof(IUnitOfWork),
unitOfWork).Create();
            }

            return;
        }

        if (context.Result is QueryResult qr)
        {
            var contextData = new Dictionary<string, object?>(context.ContextData);
            // Do not inline; this stores a reference to the request because it is set to null on the context eventually
            var contextRequest = context.Request;
            context.Result = new ResponseStream(() => LiveResults(contextData, qr, contextRequest));
        }
    }

    private IReadOnlyCollection<EventListener> GetCurrentListeners(IReadOnlyDictionary<string, object?>? contextData)
    {
        if (contextData == null)
        {
            logger.LogWarning("Trying to add live results but {ContextData} is null!", nameof(contextData));
            throw new InvalidOperationException("Cannot fetch ReadModels from null context.");
        }

        if (!contextData.TryGetValue(nameof(IUnitOfWork), out var value) || value is not IUnitOfWork unitOfWork)
        {
            logger.LogWarning("Trying to add live results but {ContextData} does not contain UnitOfWork!", nameof(contextData));
            throw new InvalidOperationException("Cannot fetch ReadModels when no UnitOfWork is present.");
        }

        return unitOfWork.ReadModels.ToList().AsReadOnly();
    }

    private async IAsyncEnumerable<IQueryResult> LiveResults(IReadOnlyDictionary<string, object?>? contextData, QueryResult firstResult, IQueryRequest originalRequest)
    {
        if (contextData == null)
        {
            logger.LogWarning("Trying to add live results but {ContextData} is null!", nameof(contextData));
            yield break;
        }

        var listeners = GetCurrentListeners(contextData);
        yield return firstResult;

        using var serviceScope = rootServiceProvider.CreateScope();
        var serviceProvider = serviceScope.ServiceProvider;

        contextData.TryGetValue(nameof(HttpContext), out var httpContext);
        var isWebsocket = (httpContext as HttpContext)?.WebSockets.IsWebSocketRequest ?? false;
        var foundSocketSession = contextData.TryGetValue(nameof(ISocketSession), out var contextSocketSession); // as ISocketSession
        var foundOperationId = contextData.TryGetValue("HotChocolate.Execution.Transport.OperationSessionId", out var operationId); // as string

        switch (isWebsocket)
        {
            case true when !foundSocketSession || !foundOperationId:
                logger.LogWarning("Trying to add live results but {SocketSession} or {OperationId} is not present in context!", nameof(contextSocketSession), nameof(operationId));
                yield break;
            case true when contextSocketSession is not ISocketSession:
                logger.LogWarning("{ContextSocketSession} key present in context but not an {ISocketSession}!", contextSocketSession?.GetType().FullName, nameof(ISocketSession));
                yield break;
        }

        ActiveLiveQueries.Add(1);

        var loopCancellation = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                if (loopCancellation.IsCancellationRequested) return;
                if (IsCancelled())
                {
                    loopCancellation.Cancel();
                    return;
                }
                await Task.Delay(5_000, loopCancellation.Token);
            }
        }, loopCancellation.Token);

        while (true)
        {
            var latestPersistedEventVersion = await WaitForChange(serviceProvider, listeners, loopCancellation.Token);

            var readOnlyQueryRequest = QueryRequestBuilder
                    .From(originalRequest)
                    .AddExtension(SubsequentRequestMarker, SubsequentRequestMarker)
                    .AddGlobalState(UnitOfWorkParameterExpressionBuilder.PrefillUnitOfWorkVersion,
                        latestPersistedEventVersion)
                    .SetServices(serviceProvider)
                    .Create();

            await using var executionResult = await serviceProvider.ExecuteRequestAsync(readOnlyQueryRequest);

            if (executionResult is not IQueryResult result
                || contextSocketSession is ISocketSession { Connection.IsClosed: true })
            {
                break;
            }

            listeners = GetCurrentListeners(executionResult.ContextData);
            yield return result;

            if (result.Errors?.Count > 0)
            {
                break;
            }
        }

        ActiveLiveQueries.Add(-1);
        yield break;

        bool IsCancelled()
        {
            return isWebsocket && contextSocketSession is ISocketSession socketSession &&
                   socketSession.Operations.All(operationSession => operationSession.Id != operationId?.ToString());
        }
    }

    /**
     * Returns the received latest persistent event version after a change has occured.
     */
    private static async ValueTask<long> WaitForChange(IServiceProvider serviceProvider, IEnumerable<EventListener> eventListeners, CancellationToken ct)
    {
        var currentEventListener = eventListeners.ToList();

        var newEventNotifier = serviceProvider.GetRequiredService<NewEventNotifier>();
        var newTransientEventNotifier = serviceProvider.GetRequiredService<NewTransientEventNotifier>();
        var eventListenerFactory = serviceProvider.GetRequiredService<EventListenerFactory>();

        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var latestPersistedEventVersion = currentEventListener.Min(el => el.LastSeenEvent);
        var latestTransientEventVersion = currentEventListener.Min(el => el.LastSeenTransientEvent);

        var persistedEventTask = Task.Run(async () =>
        {
            while (true)
            {
                latestPersistedEventVersion = await newEventNotifier.WaitForEventAfter(latestPersistedEventVersion, cancellationTokenSource.Token);

                for (var index = 0; index < currentEventListener.Count; index++)
                {
                    var eventListener = currentEventListener[index];
                    var updatedEventListener = await eventListenerFactory.UpdateTo(eventListener, latestPersistedEventVersion);

                    if (updatedEventListener.LastAcceptedEvent > eventListener.LastAcceptedEvent)
                    {
                        return;
                    }

                    currentEventListener[index] = updatedEventListener;
                }
            }
        }, cancellationTokenSource.Token);

        var transientEventTask = Task.Run(async () =>
        {
            while (true)
            {
                var events = (await newTransientEventNotifier.WaitForEventAfter(latestTransientEventVersion, cancellationTokenSource.Token)).ToList();

                for (var index = 0; index < currentEventListener.Count; index++)
                {
                    var eventListener = currentEventListener[index];
                    var updatedEventListener = eventListenerFactory.UpdateWithEvents(eventListener, events.ToPagedMemory());

                    if (updatedEventListener != eventListener)
                    {
                        return;
                    }

                    currentEventListener[index] = updatedEventListener;
                }

                latestTransientEventVersion = events.Max(el => el.Version);
            }
        }, cancellationTokenSource.Token);

        var completedTask = await Task.WhenAny(persistedEventTask, transientEventTask);
        cancellationTokenSource.Cancel();

        if (completedTask.IsFaulted)
        {
            throw completedTask.Exception!;
        }

        return latestPersistedEventVersion;
    }
}
