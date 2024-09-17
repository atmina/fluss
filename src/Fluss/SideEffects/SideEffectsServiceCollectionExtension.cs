using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.SideEffects;

public static class SideEffectsServiceCollectionExtension
{
    public static IServiceCollection AddSideEffect<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSideEffect>(this IServiceCollection services) where TSideEffect : class, SideEffect
    {
        return services.AddSingleton<SideEffect, TSideEffect>();
    }
}
