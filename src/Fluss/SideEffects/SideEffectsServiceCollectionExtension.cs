using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.SideEffects;

public static class SideEffectsServiceCollectionExtension
{
    public static IServiceCollection RegisterSideEffects(this IServiceCollection services, Assembly assembly)
    {
        var implementingClasses = assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(SideEffect))).ToList();

        foreach (var @class in implementingClasses)
        {
            services.AddScoped(typeof(SideEffect), @class);
        }

        services.AddHostedService<SideEffectDispatcher>();

        return services;
    }
}
