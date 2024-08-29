using System.Reflection;
using System.Runtime.CompilerServices;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.Events.TransientEvents;
using Fluss.SideEffects;
using Fluss.UnitOfWork;
using Fluss.Upcasting;
using Fluss.Validation;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("Fluss.UnitTest")]
[assembly: InternalsVisibleTo("Fluss.HotChocolate")]
[assembly: InternalsVisibleTo("Fluss.Testing")]

namespace Fluss;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventSourcing(this IServiceCollection services, bool addCaching = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddBaseEventRepository<InMemoryEventRepository>()
            .AddSingleton<EventListenerFactory>()
            .AddSingleton<EventListenerFactoryPipeline, InMemoryEventListenerCache>()
            .AddSingleton(sp =>
            {
                var pipeline = sp.GetServices<EventListenerFactoryPipeline>();
                IEventListenerFactory eventListenerFactory = sp.GetRequiredService<EventListenerFactory>();
                foreach (var pipelineItem in pipeline)
                {
                    pipelineItem.Next = eventListenerFactory;
                    eventListenerFactory = pipelineItem;
                }

                return eventListenerFactory;
            })
            .AddSingleton<ArbitraryUserUnitOfWorkCache>()
            .AddTransient<Fluss.UnitOfWork.UnitOfWork>()
            .AddTransient<IUnitOfWork>(sp => sp.GetRequiredService<Fluss.UnitOfWork.UnitOfWork>())
            .AddTransient<UnitOfWorkFactory>();

        if (addCaching)
        {
            services.AddEventRepositoryPipeline<InMemoryEventCache>();
        }

        services
            .AddEventRepositoryPipeline<TransientEventAwareEventRepository>()
            .AddSingleton<EventListenerFactoryPipeline, TransientEventAwareEventListenerFactory>();

        return services;
    }

    public static IServiceCollection AddEventRepositoryPipeline<TEventRepository>(this IServiceCollection services)
        where TEventRepository : EventRepositoryPipeline
    {
        return services
            .AddSingleton<TEventRepository>()
            .AddSingleton<EventRepositoryPipeline>(sp => sp.GetRequiredService<TEventRepository>());
    }

    public static IServiceCollection AddBaseEventRepository<TBaseEventRepository>(this IServiceCollection services)
        where TBaseEventRepository : class, IBaseEventRepository
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        return services
            .AddSingleton<IBaseEventRepository, TBaseEventRepository>()
            .AddSingleton(sp =>
            {
                var pipeline = sp.GetServices<EventRepositoryPipeline>();
                IEventRepository eventRepository = sp.GetRequiredService<IBaseEventRepository>();
                foreach (var pipelineItem in pipeline)
                {
                    pipelineItem.Next = eventRepository;
                    eventRepository = pipelineItem;
                }

                return eventRepository;
            });
    }

    public static IServiceCollection AddUpcasters(this IServiceCollection services, Assembly sourceAssembly)
    {
        var upcasterType = typeof(IUpcaster);
        var upcasters = sourceAssembly.GetTypes().Where(t => t.IsAssignableTo(upcasterType));

        foreach (var upcaster in upcasters)
        {
            services.AddSingleton(upcasterType, upcaster);
        }

        return services.AddSingleton<EventUpcasterService>().AddSingleton<UpcasterSorter>();
    }
}
