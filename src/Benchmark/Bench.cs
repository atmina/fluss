using BenchmarkDotNet_GitCompare;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Fluss;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.PostgreSQL;
using Fluss.ReadModel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Benchmark;

[GitJob("HEAD", baseline: true, id: "0_before", iterationCount: 10)]
[SimpleJob(id: "1_after", iterationCount: 10)]
[RPlotExporter]
[MemoryDiagnoser]
public class Bench
{
    [Params("postgres")]
    public string StorageType { get; set; } = "in-memory";

    [IterationSetup]
    public void Setup()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();

        if (StorageType == "postgres")
        {
            sc.AddPostgresEventSourcingRepository("Host=localhost;Port=5432;Database=fluss;Username=fluss;Password=fluss");
        }

        sc.AddPolicy<AllowAllPolicy>();

        _sp = sc.BuildServiceProvider();

        if (StorageType == "postgres")
        {
            var migrator = _sp.GetRequiredService<Migrator>();
            migrator.StartAsync(default).Wait();
            migrator.WaitForFinish().Wait();
        }
    }

    ServiceProvider _sp = null!;

    [Benchmark]
    public async Task<int> PublishEventsAndReadMixedReadWrite()
    {
        var sum = 0;

        var limit = StorageType == "postgres" ? 4 : 1000;
        for (var j = 0; j < limit; j++)
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

    [IterationCleanup]
    public void Cleanup()
    {
        if (StorageType == "postgres")
        {
            var conn = new NpgsqlConnection("Host=localhost;Port=5432;Database=fluss;Username=fluss;Password=fluss");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM ""Events""";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
    }

    [IterationSetup(Targets = [nameof(PublishEventsAndReadReadHeavySingleReadModel), nameof(PublishEventsAndReadReadHeavyMultipleReadModel)])]
    public void SetupHeavyRead()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();

        if (StorageType == "postgres")
        {
            sc.AddPostgresEventSourcingRepository("Host=localhost;Port=5432;Database=fluss;Username=fluss;Password=fluss");
        }

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

        var limit = StorageType == "postgres" ? 1000 : 50000;

        for (var j = 0; j < limit; j++)
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

        var limit = StorageType == "postgres" ? 400 : 5000;
        for (var j = 1; j < limit; j++)
        {
            using var unitOfWork = _sp.GetSystemUserUnitOfWork();
            var readModel1 = await unitOfWork.GetReadModel<EventsModEqualReadModel, int>(j);
            sum += readModel1.GotEvents;
        }

        return sum;
    }

    [IterationSetup(Target = nameof(SuperHeavyParallelRead))]
    public void SetupSuperHeavyParallelRead()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();

        if (StorageType == "postgres")
        {
            sc.AddPostgresEventSourcingRepository("Host=localhost;Port=5432;Database=fluss;Username=fluss;Password=fluss");
        }

        sc.AddPolicy<AllowAllPolicy>();

        _sp = sc.BuildServiceProvider();

        _sp.GetSystemUserUnitOfWorkFactory().Commit(async unitOfWork =>
        {
            for (var i = 0; i < 200_000; i++)
            {
                await unitOfWork.Publish(new TestEvent(i));
            }
        }).AsTask().Wait();
    }

    [Benchmark]
    public async Task<int> SuperHeavyParallelRead()
    {
        var sum = 0;

        const int minKey = 0;
        var maxKey = StorageType == "postgres" ? 800 : 4000;
        const int parallelCount = 100;
        var blockSize = (maxKey - minKey) / parallelCount;

        var blocks = Enumerable.Range(0, parallelCount)
            .Select(i => Enumerable.Range(i * blockSize + 1, blockSize));

        var tasks = blocks.Select(async b =>
        {
            using var unitOfWork = _sp.GetSystemUserUnitOfWork();
            var readModel1 = await unitOfWork.GetMultipleReadModels<EventsModEqualReadModel, int>(b);
            sum += readModel1.Sum(e => e.GotEvents);
        });

        await Task.WhenAll(tasks);

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
