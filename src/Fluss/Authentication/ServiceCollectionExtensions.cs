using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.Authentication;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPoliciesFrom(this IServiceCollection services, Assembly assembly)
    {
        var policyTypes = assembly.GetTypes().Where(x => x.IsAssignableTo(typeof(Policy)));

        foreach (var policyType in policyTypes)
        {
            services
                .AddSingleton(policyType)
                .AddSingleton(sp => (Policy)sp.GetRequiredService(policyType));
        }

        return services;
    }

    public static IServiceCollection ProvideUserIdFrom(this IServiceCollection services,
        Func<IServiceProvider, Guid> func)
    {
        return services.AddSingleton(sp => new UserIdProvider(func, sp));
    }
}
