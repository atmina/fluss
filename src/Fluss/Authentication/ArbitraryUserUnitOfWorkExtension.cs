using System.Collections.Concurrent;
using Fluss.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.Authentication;

public interface IArbitraryUserUnitOfWorkCache
{
    UnitOfWorkFactory GetUserUnitOfWorkFactory(Guid userId);
    IUnitOfWork GetUserUnitOfWork(Guid userId);
}

public class ArbitraryUserUnitOfWorkCache : IArbitraryUserUnitOfWorkCache
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Guid, IServiceProvider> _cache = new();

    public ArbitraryUserUnitOfWorkCache(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public UnitOfWorkFactory GetUserUnitOfWorkFactory(Guid userId)
    {
        var sp = GetCachedServiceProvider(userId);
        return sp.GetRequiredService<UnitOfWorkFactory>();
    }

    public IUnitOfWork GetUserUnitOfWork(Guid userId)
    {
        var sp = GetCachedServiceProvider(userId);
        return sp.GetRequiredService<UnitOfWork.UnitOfWork>();
    }

    private IServiceProvider GetCachedServiceProvider(Guid userId)
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
            collection.AddSingleton(type, _serviceProvider.GetRequiredService(type));
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
        return serviceProvider.GetRequiredService<IArbitraryUserUnitOfWorkCache>().GetUserUnitOfWorkFactory(userId);
    }

    public static IUnitOfWork GetUserUnitOfWork(this IServiceProvider serviceProvider, Guid userId)
    {
        return serviceProvider.GetRequiredService<IArbitraryUserUnitOfWorkCache>().GetUserUnitOfWork(userId);
    }
}
