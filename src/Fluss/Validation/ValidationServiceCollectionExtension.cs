using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.Core.Validation;

public static class ValidationServiceCollectionExtension
{
    public static IServiceCollection AddValidationFrom(this IServiceCollection services, Assembly sourceAssembly)
    {
        var aggregateValidatorType = typeof(AggregateValidator);
        var aggregateValidators =
            sourceAssembly.GetTypes().Where(t => t.IsAssignableTo(aggregateValidatorType)).ToList();
        foreach (var aggregateValidator in aggregateValidators)
        {
            services.AddSingleton(aggregateValidatorType, aggregateValidator);
        }

        var eventValidatorType = typeof(EventValidator);
        var eventValidators =
            sourceAssembly.GetTypes().Where(t => t.IsAssignableTo(eventValidatorType)).ToList();
        foreach (var eventValidator in eventValidators)
        {
            services.AddSingleton(eventValidatorType, eventValidator);
        }

        services.AddSingleton<IRootValidator, RootValidator>();

        return services;
    }
}
