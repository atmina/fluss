using System.Collections.Concurrent;
using Fluss.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.Authentication;

public class ArbitraryUserUnitOfWorkCache
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Guid, IServiceProvider> _cache = new();

    public ArbitraryUserUnitOfWorkCache(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public UnitOfWorkFactory GetUserUnitOfWorkFactory(Guid userId)
    {
        var sp = GetCachedUserUnitOfWork(userId);
        return sp.GetRequiredService<UnitOfWorkFactory>();
    }

    public UnitOfWork.UnitOfWork GetUserUnitOfWork(Guid userId)
    {
        var sp = GetCachedUserUnitOfWork(userId);
        return sp.GetRequiredService<UnitOfWork.UnitOfWork>();
    }

    private IServiceProvider GetCachedUserUnitOfWork(Guid userId)
    {
        return _cache.GetOrAdd(userId, CreateUserServiceProvider);
    }

    private IServiceProvider CreateUserServiceProvider(Guid providedId)
    {
        var collection = new ServiceCollection();
        var constructorArgumentTypes = typeof(UnitOfWork.UnitOfWork).GetConstructors().Single().GetParameters()
            .Select(p => p.ParameterType);

        foreach (var type in constructorArgumentTypes)
        {
            if (type == typeof(UserIdProvider)) continue;
            collection.AddSingleton(type, _serviceProvider.GetService(type)!);
        }

        collection.ProvideUserIdFrom(_ => providedId);
        collection.AddTransient<UnitOfWork.UnitOfWork>();
        collection.AddTransient<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork.UnitOfWork>());
        collection.AddTransient<UnitOfWorkFactory>();

        return collection.BuildServiceProvider();
    }
}

public static class ArbitraryUserUnitOfWorkExtension
{
    public static UnitOfWorkFactory GetUserUnitOfWorkFactory(this IServiceProvider serviceProvider, Guid userId)
    {
        return serviceProvider.GetRequiredService<ArbitraryUserUnitOfWorkCache>().GetUserUnitOfWorkFactory(userId);
    }

    public static UnitOfWork.UnitOfWork GetUserUnitOfWork(this IServiceProvider serviceProvider, Guid userId)
    {
        return serviceProvider.GetRequiredService<ArbitraryUserUnitOfWorkCache>().GetUserUnitOfWork(userId);
    }
}
