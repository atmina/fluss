using Fluss.Authentication;
using Fluss.Events;
using Fluss.Events.TransientEvents;
using Fluss.SideEffects;
using Fluss.Upcasting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fluss.UnitTest.Core.SideEffects;

public class DispatcherTest
{
    [Fact]
    public async Task DispatchesSideEffectHandlerOnNewEvent()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventSourcing(false)
            .ProvideUserIdFrom(_ => Guid.Empty)
            .AddBaseEventRepository<InMemoryEventRepository>()
            .AddSingleton<Policy, AllowAllPolicy>()
            .AddSingleton<TestSideEffect>()
            .AddSingleton<SideEffect>(sp => sp.GetRequiredService<TestSideEffect>());

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<SideEffectDispatcher>()
            .Single();

        var upcaster = serviceProvider.GetRequiredService<EventUpcasterService>();
        await upcaster.Run();

        await dispatcher.StartAsync(CancellationToken.None);

        await serviceProvider.GetRequiredService<UnitOfWorkFactory>().Commit(async unitOfWork =>
        {
            await unitOfWork.Publish(new TestEvent());
        });

        var testSideEffect = serviceProvider.GetRequiredService<TestSideEffect>();

        await WaitUntilTrue(() => testSideEffect.DidTrigger, TimeSpan.FromMilliseconds(100));

        Assert.True(testSideEffect.DidTrigger);

        await dispatcher.StopAsync(CancellationToken.None);
    }

    private class TestEvent : Event;

    private class AllowAllPolicy : Policy
    {
        public ValueTask<bool> AuthenticateEvent(EventEnvelope envelope, IAuthContext authContext)
        {
            return ValueTask.FromResult(true);
        }
    }

    private class TestSideEffect : SideEffect<TestEvent>
    {
        public bool DidTrigger { get; private set; }

        public Task<IEnumerable<Event>> HandleAsync(TestEvent @event, Fluss.UnitOfWork unitOfWork)
        {
            DidTrigger = true;
            return Task.FromResult<IEnumerable<Event>>(Array.Empty<Event>());
        }
    }

    [Fact]
    public async Task DispatchesSideEffectHandlerOnNewTransientEvent()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventSourcing(false)
            .ProvideUserIdFrom(_ => Guid.Empty)
            .AddBaseEventRepository<InMemoryEventRepository>()
            .AddSingleton<Policy, AllowAllPolicy>()
            .AddSingleton<TestTransientSideEffect>()
            .AddSingleton<SideEffect>(sp => sp.GetRequiredService<TestTransientSideEffect>());

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<SideEffectDispatcher>()
            .Single();

        var upcaster = serviceProvider.GetRequiredService<EventUpcasterService>();
        await upcaster.Run();

        await dispatcher.StartAsync(CancellationToken.None);

        await serviceProvider.GetRequiredService<UnitOfWorkFactory>().Commit(async unitOfWork =>
        {
            await unitOfWork.Publish(new TestTransientEvent());
        });

        var testTransientSideEffect = serviceProvider.GetRequiredService<TestTransientSideEffect>();

        await WaitUntilTrue(() => testTransientSideEffect.DidTrigger, TimeSpan.FromMilliseconds(100));

        Assert.True(testTransientSideEffect.DidTrigger);

        await dispatcher.StopAsync(CancellationToken.None);
    }

    private class TestTransientEvent : TransientEvent;

    private class TestTransientSideEffect : SideEffect<TestTransientEvent>
    {
        public bool DidTrigger { get; set; }

        public Task<IEnumerable<Event>> HandleAsync(TestTransientEvent @event,
            Fluss.UnitOfWork unitOfWork)
        {
            DidTrigger = true;
            return Task.FromResult<IEnumerable<Event>>(Array.Empty<Event>());
        }
    }

    [Fact]
    public async Task PublishesNewEventsReturnedBySideEffect()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventSourcing(false)
            .ProvideUserIdFrom(_ => Guid.Empty)
            .AddBaseEventRepository<InMemoryEventRepository>()
            .AddSingleton<Policy, AllowAllPolicy>()
            .AddSingleton<TestReturningSideEffect>()
            .AddSingleton<SideEffect>(sp => sp.GetRequiredService<TestReturningSideEffect>());

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<SideEffectDispatcher>()
            .Single();

        var upcaster = serviceProvider.GetRequiredService<EventUpcasterService>();
        await upcaster.Run();

        await dispatcher.StartAsync(CancellationToken.None);

        await serviceProvider.GetRequiredService<UnitOfWorkFactory>().Commit(async unitOfWork =>
        {
            await unitOfWork.Publish(new TestTriggerEvent());
        });

        var repository = serviceProvider.GetRequiredService<InMemoryEventRepository>();

        await WaitUntilTrue(async () => await repository.GetLatestVersion() >= 1, TimeSpan.FromMilliseconds(100));

        var newEvent = (await repository.GetEvents(0, 1).ToFlatEventList())[0];

        Assert.True(newEvent.Event is TestReturnedEvent);

        await dispatcher.StopAsync(CancellationToken.None);
    }

    private class TestTriggerEvent : Event;
    private class TestReturnedEvent : Event;

    private class TestReturningSideEffect : SideEffect<TestTriggerEvent>
    {
        public Task<IEnumerable<Event>> HandleAsync(TestTriggerEvent @event, Fluss.UnitOfWork unitOfWork)
        {
            return Task.FromResult<IEnumerable<Event>>([new TestReturnedEvent()]);
        }
    }

    private static async Task WaitUntilTrue(Func<Task<bool>> f, TimeSpan timeSpan)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(timeSpan);

        var cancellationToken = cancellationTokenSource.Token;

        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            var b = await f();
            if (b)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }
    }

    private Task WaitUntilTrue(Func<bool> f, TimeSpan timeSpan)
    {
        return WaitUntilTrue(() => Task.FromResult(f()), timeSpan);
    }
}