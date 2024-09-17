using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.Authentication;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPolicy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(this IServiceCollection services) where TPolicy : class, Policy
    {
        return services.AddSingleton<Policy, TPolicy>();
    }

    public static IServiceCollection ProvideUserIdFrom(this IServiceCollection services,
        Func<IServiceProvider, Guid> func)
    {
        return services.AddSingleton(sp => new UserIdProvider(func, sp));
    }
}
