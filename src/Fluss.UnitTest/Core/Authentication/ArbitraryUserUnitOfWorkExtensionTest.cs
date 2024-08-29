using Fluss.Authentication;
using Fluss.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.UnitTest.Core.Authentication;

public class ArbitraryUserUnitOfWorkExtensionTest
{
    [Fact]
    public async Task CanCreateUnitOfWorkWithArbitraryGuid()
    {
        var guid = Guid.NewGuid();
        
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventSourcing(false)
            .AddBaseEventRepository<InMemoryEventRepository>()
            .AddSingleton<Policy, AllowAllPolicy>();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // ReSharper disable once InvokeAsExtensionMethod
        var unitOfWork = ArbitraryUserUnitOfWorkExtension.GetUserUnitOfWork(serviceProvider, guid);
        await unitOfWork.Publish(new TestEvent());
        await ((Fluss.UnitOfWork.UnitOfWork) unitOfWork).CommitInternal();

        var inMemoryEventRepository = serviceProvider.GetRequiredService<InMemoryEventRepository>();
        var events = await inMemoryEventRepository.GetEvents(-1, 0);
        
        Assert.Equal(guid, events[0].ToArray()[0].By);
    }
    
    [Fact]
    public async Task CanCreateUnitOfWorkFactoryWithArbitraryGuid()
    {
        var guid = Guid.NewGuid();
        
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventSourcing(false)
            .AddBaseEventRepository<InMemoryEventRepository>()
            .AddSingleton<Policy, AllowAllPolicy>();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // ReSharper disable once InvokeAsExtensionMethod
        var unitOfWorkFactory = ArbitraryUserUnitOfWorkExtension.GetUserUnitOfWorkFactory(serviceProvider, guid);

        await unitOfWorkFactory.Commit(async work =>
        {
            await work.Publish(new TestEvent());
        });
        
        var inMemoryEventRepository = serviceProvider.GetRequiredService<InMemoryEventRepository>();
        var events = await inMemoryEventRepository.GetEvents(-1, 0);
        
        Assert.Equal(guid, events[0].ToArray()[0].By);
    }
    
    private class TestEvent : Event {}
    
    private class AllowAllPolicy : Policy
    {
        public ValueTask<bool> AuthenticateEvent(EventEnvelope envelope, IAuthContext authContext)
        {
            return ValueTask.FromResult(true);
        }
    }
}