using BenchmarkDotNet_GitCompare;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Fluss;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.ReadModel;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmark;

[GitJob("879573d", baseline: true, id: "0_before")]
[SimpleJob(id: "1_after")]
[RPlotExporter]
[MemoryDiagnoser]
public class Bench
{
    [IterationSetup]
    public void Setup()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();
        sc.AddPolicy<AllowAllPolicy>();

        _sp = sc.BuildServiceProvider();
    }

    ServiceProvider _sp = null!;

    [Benchmark]
    public async Task<int> PublishEventsAndReadMixedReadWrite()
    {
        var sum = 0;
        for (var j = 0; j < 1000; j++)
        {
            await _sp.GetSystemUserUnitOfWorkFactory().Commit(async unitOfWork =>
            {
                for (var i = 0; i < 50; i++)
                {
                    await unitOfWork.Publish(new TestEvent(1));
                    await unitOfWork.Publish(new TestEvent(2));
                }
            });

            using var unitOfWork = _sp.GetSystemUserUnitOfWork();
            var readModel1 = await unitOfWork.GetReadModel<EventsEqualReadModel, int>(1);
            var readModel2 = await unitOfWork.GetReadModel<EventsEqualReadModel, int>(2);
            sum += readModel1.GotEvents + readModel2.GotEvents;
        }

        return sum;
    }

    [IterationSetup(Targets = [nameof(PublishEventsAndReadReadHeavySingleReadModel), nameof(PublishEventsAndReadReadHeavyMultipleReadModel)])]
    public void SetupHeavyRead()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();
        sc.AddPolicy<AllowAllPolicy>();

        _sp = sc.BuildServiceProvider();

        _sp.GetSystemUserUnitOfWorkFactory().Commit(async unitOfWork =>
        {
            for (var i = 0; i < 10000; i++)
            {
                await unitOfWork.Publish(new TestEvent(i));
            }
        }).AsTask().Wait();
    }


    [Benchmark]
    public async Task<int> PublishEventsAndReadReadHeavySingleReadModel()
    {
        var sum = 0;

        for (var j = 0; j < 50000; j++)
        {
            using var unitOfWork = _sp.GetSystemUserUnitOfWork();
            var readModel1 = await unitOfWork.GetReadModel<EventsModEqualReadModel, int>(3);
            sum += readModel1.GotEvents;
        }

        return sum;
    }

    [Benchmark]
    public async Task<int> PublishEventsAndReadReadHeavyMultipleReadModel()
    {
        var sum = 0;

        for (var j = 1; j < 5000; j++)
        {
            using var unitOfWork = _sp.GetSystemUserUnitOfWork();
            var readModel1 = await unitOfWork.GetReadModel<EventsModEqualReadModel, int>(j);
            sum += readModel1.GotEvents;
        }

        return sum;
    }
}

public class AllowAllPolicy : Policy
{
    public ValueTask<bool> AuthenticateEvent(EventEnvelope envelope, IAuthContext authContext)
    {
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> AuthenticateReadModel(IReadModel readModel, IAuthContext authContext)
    {
        return ValueTask.FromResult(true);
    }
}

public record TestEvent(int Id) : Event;

public record EventsEqualReadModel : ReadModelWithKey<int>
{
    public int GotEvents { get; private init; }

    protected override EventsEqualReadModel When(EventEnvelope envelope)
    {
        return envelope.Event switch
        {
            TestEvent testEvent when testEvent.Id == Id => this with { GotEvents = GotEvents + 1 },
            _ => this,
        };
    }
}

public record EventsModEqualReadModel : ReadModelWithKey<int>
{
    public int GotEvents { get; private init; }

    protected override EventsModEqualReadModel When(EventEnvelope envelope)
    {
        return envelope.Event switch
        {
            TestEvent testEvent when testEvent.Id % Id == 0 => this with { GotEvents = GotEvents + 1 },
            _ => this,
        };
    }
}
