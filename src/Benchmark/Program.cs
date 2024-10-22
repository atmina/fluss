using Benchmark;
using BenchmarkDotNet.Running;
using Fluss;
using Fluss.Authentication;
using Microsoft.Extensions.DependencyInjection;

var sc = new ServiceCollection();
sc.AddEventSourcing();
sc.AddPolicy<AllowAllPolicy>();

var sp = sc.BuildServiceProvider();
for (var i = 0; i < 1500; i++)
{
    await sp.GetSystemUserUnitOfWorkFactory().Commit(async uow =>
    {
        await uow.Publish(new TestEvent(i));
    });
}

await Task.Delay(3000);

// BenchmarkRunner.Run<Bench>();
