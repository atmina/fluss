using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using Fluss.Events;
using Fluss.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.Authentication;

public interface IArbitraryUserUnitOfWorkCache
{
    UnitOfWorkFactory GetUserUnitOfWorkFactory(Guid userId);
    IUnitOfWork GetUserUnitOfWork(Guid userId);
}

public class ArbitraryUserUnitOfWorkCache(IServiceProvider serviceProvider) : IArbitraryUserUnitOfWorkCache
{
    private readonly ConcurrentDictionary<Guid, IServiceProvider> _cache = new();

    public UnitOfWorkFactory GetUserUnitOfWorkFactory(Guid userId)
    {
        var sp = GetCachedServiceProvider(userId);
        return sp.GetRequiredService<UnitOfWorkFactory>();
    }

    [Pure]
    public IUnitOfWork GetUserUnitOfWork(Guid userId)
    {
        var sp = GetCachedServiceProvider(userId);
        return sp.GetRequiredService<UnitOfWork>();
    }

    private IServiceProvider GetCachedServiceProvider(Guid userId)
    {
        return _cache.TryGetValue(userId, out var value) ? value : _cache.GetOrAdd(userId, CreateUserServiceProvider);
    }

    private IServiceProvider CreateUserServiceProvider(Guid providedId)
    {
        var collection = new ServiceCollection();

        collection.AddSingleton<IEventRepository>(_ => serviceProvider.GetRequiredService<IEventRepository>());
        collection.AddSingleton<IEventListenerFactory>(_ => serviceProvider.GetRequiredService<IEventListenerFactory>());
        collection.AddSingleton<IEnumerable<Policy>>(_ => serviceProvider.GetRequiredService<IEnumerable<Policy>>());
        collection.AddSingleton<IRootValidator>(_ => serviceProvider.GetRequiredService<IRootValidator>());

        collection.ProvideUserIdFrom(_ => providedId);
        collection.AddTransient<UnitOfWork>(CreateUnitOfWork);
        collection.AddTransient<IUnitOfWork>(CreateUnitOfWork);
        collection.AddTransient<UnitOfWorkFactory>();

        return collection.BuildServiceProvider();
    }

    private UnitOfWork CreateUnitOfWork(IServiceProvider sp)
    {
        return UnitOfWork.Create(
            sp.GetRequiredService<IEventRepository>(),
            sp.GetRequiredService<IEventListenerFactory>(),
            sp.GetServices<Policy>(),
            sp.GetRequiredService<UserIdProvider>(),
            sp.GetRequiredService<IRootValidator>()
        );
    }
}

public static class ArbitraryUserUnitOfWorkExtension
{
    public static UnitOfWorkFactory GetUserUnitOfWorkFactory(this IServiceProvider serviceProvider, Guid userId)
    {
        return serviceProvider.GetRequiredService<IArbitraryUserUnitOfWorkCache>().GetUserUnitOfWorkFactory(userId);
    }

    /// <summary>
    /// Returns a UnitOfWork that is configured to use the provided Guid for determining the current user.
    ///
    /// <b>You MUST call .Return() on the result of this function once it's not required any more.</b>
    /// </summary>
    /// <param name="serviceProvider">this</param>
    /// <param name="userId">The id of the user that the UnitOfWork should use for authorization.</param>
    /// <returns>A UnitOfWork that is configured to use the provided Guid for determining the current user.</returns>
    public static IUnitOfWork GetUserUnitOfWork(this IServiceProvider serviceProvider, Guid userId)
    {
        return serviceProvider.GetRequiredService<IArbitraryUserUnitOfWorkCache>().GetUserUnitOfWork(userId);
    }
}
