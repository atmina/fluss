using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.Validation;

public static class ValidationServiceCollectionExtension
{
    public static IServiceCollection AddAggregateValidator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAggregateValidator>(this IServiceCollection services) where TAggregateValidator : class, AggregateValidator
    {
        return services.AddSingleton<AggregateValidator, TAggregateValidator>();
    }

    public static IServiceCollection AddEventValidator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TEventValidator>(this IServiceCollection services) where TEventValidator : class, EventValidator
    {
        return services.AddSingleton<EventValidator, TEventValidator>();
    }
}
