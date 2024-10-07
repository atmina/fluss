using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.Events.TransientEvents;
using Fluss.SideEffects;
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
            .AddLogging()
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
            .AddSingleton<IArbitraryUserUnitOfWorkCache, ArbitraryUserUnitOfWorkCache>()
            .AddTransient<UnitOfWork>(CreateNewUnitOfWork)
            .AddTransient<IUnitOfWork>(CreateNewUnitOfWork)
            .AddTransient<UnitOfWorkFactory>()
            .AddSingleton<IRootValidator, RootValidator>()
            .AddHostedService<SideEffectDispatcher>()
            .AddSingleton<EventUpcasterService>()
            .AddSingleton<UpcasterSorter>();

        if (addCaching)
        {
            services.AddEventRepositoryPipeline<InMemoryEventCache>();
        }

        services
            .AddEventRepositoryPipeline<TransientEventAwareEventRepository>()
            .AddSingleton<EventListenerFactoryPipeline, TransientEventAwareEventListenerFactory>();

        return services;
    }

    private static UnitOfWork CreateNewUnitOfWork(IServiceProvider sp)
    {
        return UnitOfWork.Create(
            sp.GetRequiredService<IEventRepository>(),
            sp.GetRequiredService<IEventListenerFactory>(),
            sp.GetServices<Policy>(),
            sp.GetRequiredService<UserIdProvider>(),
            sp.GetRequiredService<IRootValidator>()
        );
    }

    public static IServiceCollection AddEventRepositoryPipeline<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TEventRepository>(
        this IServiceCollection services)
        where TEventRepository : EventRepositoryPipeline
    {
        return services
            .AddSingleton<TEventRepository>()
            .AddSingleton<EventRepositoryPipeline>(sp => sp.GetRequiredService<TEventRepository>());
    }

    public static IServiceCollection AddBaseEventRepository<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBaseEventRepository>(
        this IServiceCollection services)
        where TBaseEventRepository : class, IBaseEventRepository
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddSingleton<TBaseEventRepository>()
            .AddSingleton<IBaseEventRepository, TBaseEventRepository>(sp =>
                sp.GetRequiredService<TBaseEventRepository>())
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

    public static IServiceCollection AddUpcaster<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TUpcaster>(
        this IServiceCollection services) where TUpcaster : class, IUpcaster
    {
        return services.AddSingleton<IUpcaster, TUpcaster>();
    }
}