using Fluss.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace Fluss;

public class UnitOfWorkFactory
{
    private readonly IServiceProvider _serviceProvider;

    public UnitOfWorkFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private static readonly IList<TimeSpan> Delay = Backoff
        .DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromMilliseconds(1), retryCount: 100)
        .Select(s => TimeSpan.FromTicks(Math.Min(s.Ticks, TimeSpan.FromSeconds(1).Ticks))).ToList();

    private static readonly AsyncRetryPolicy RetryPolicy = Policy
        .Handle<RetryException>()
        .WaitAndRetryAsync(Delay);

    public async ValueTask Commit(Func<UnitOfWork, ValueTask> action)
    {
        using var activity = FlussActivitySource.Source.StartActivity();

        await RetryPolicy
            .ExecuteAsync(async () =>
            {
                var unitOfWork = _serviceProvider.GetRequiredService<UnitOfWork>();
                await action(unitOfWork);
                await unitOfWork.CommitInternal();
            });
    }

    public async ValueTask<T> Commit<T>(Func<UnitOfWork, ValueTask<T>> action)
    {
        using var activity = FlussActivitySource.Source.StartActivity();

        return await RetryPolicy
            .ExecuteAsync(async () =>
            {
                var unitOfWork = _serviceProvider.GetRequiredService<UnitOfWork>();
                var result = await action(unitOfWork);
                await unitOfWork.CommitInternal();
                return result;
            });
    }
}
