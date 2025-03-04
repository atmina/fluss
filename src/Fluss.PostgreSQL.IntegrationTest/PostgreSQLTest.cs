using Fluss.Events;
using Fluss.Upcasting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using Xunit.Abstractions;

namespace Fluss.PostgreSQL.IntegrationTest;

public class PostgreSQLTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _testOutputHelper;
    private string _dbName = null!;
    private string _managementConnectionString = null!;
    private string _connectionString = null!;

    public PostgreSQLTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public async Task InitializeAsync()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, false)
            .Build();

        _managementConnectionString = config.GetConnectionString("DefaultConnection")!;
        _dbName = "test" + Guid.NewGuid().ToString().Replace("-", "");

        await using var npgsqlConnection = new NpgsqlConnection(_managementConnectionString);
        await npgsqlConnection.OpenAsync();

        await using var command = new NpgsqlCommand("CREATE DATABASE " + _dbName, npgsqlConnection);
        await command.ExecuteNonQueryAsync();

        _connectionString = new NpgsqlConnectionStringBuilder(_managementConnectionString)
        {
            Database = _dbName
        }.ConnectionString;
    }

    [Fact]
    public async Task SimpleTest()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();
        sc.AddPostgresEventSourcingRepository(_connectionString);

        var sp = sc.BuildServiceProvider();

        await sp.GetRequiredService<Migrator>().StartAsync(default);
        await sp.GetRequiredService<Upcaster>().StartAsync(default);
        await sp.GetRequiredService<Migrator>().WaitForFinish();

        var eventRepository = sp.GetRequiredService<IEventRepository>();
        await eventRepository.Publish([
            new EventEnvelope
            {
                Event = new TestEvent(42),
                Version = 0,
                At = DateTimeOffset.UtcNow,
                By = null
            }
        ]);

        var version = await eventRepository.GetLatestVersion();
        Assert.Equal(0, version);

        var events = await eventRepository.GetEvents(-1, 0);
        Assert.Single(events);

        Assert.Equal(new TestEvent(42), events[0].Span[0].Event);
    }

    [Fact]
    public async Task TestGetRawEvents()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();
        sc.AddPostgresEventSourcingRepository(_connectionString);

        var sp = sc.BuildServiceProvider();

        await sp.GetRequiredService<Migrator>().StartAsync(default);
        await sp.GetRequiredService<Upcaster>().StartAsync(default);
        await sp.GetRequiredService<Migrator>().WaitForFinish();

        var eventRepository = sp.GetRequiredService<IEventRepository>();
        var baseEventRepository = sp.GetRequiredService<PostgreSQLEventRepository>();

        // Publish some events
        await eventRepository.Publish([
            new EventEnvelope
            {
                Event = new TestEvent(1),
                Version = 0,
                At = DateTimeOffset.UtcNow,
                By = null
            },
            new EventEnvelope
            {
                Event = new TestEvent(2),
                Version = 1,
                At = DateTimeOffset.UtcNow,
                By = null
            }
        ]);

        // Get raw events
        var rawEvents = await baseEventRepository.GetRawEvents();
        var eventList = rawEvents.ToList();

        Assert.Equal(2, eventList.Count);
        Assert.Equal(0, eventList[0].Version);
        Assert.Equal(1, eventList[1].Version);
    }

    [Fact]
    public async Task TestReplaceEvent()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();
        sc.AddPostgresEventSourcingRepository(_connectionString);

        var sp = sc.BuildServiceProvider();

        await sp.GetRequiredService<Migrator>().StartAsync(default);
        await sp.GetRequiredService<Upcaster>().StartAsync(default);
        await sp.GetRequiredService<Migrator>().WaitForFinish();

        var baseEventRepository = (PostgreSQLEventRepository)sp.GetRequiredService<IBaseEventRepository>();

        // Publish an event
        await baseEventRepository.Publish([
            new EventEnvelope
            {
                Event = new TestEvent(1),
                Version = 0,
                At = DateTimeOffset.UtcNow,
                By = null
            }
        ]);

        // Replace the event
        var newEvent = new RawEventEnvelope
        {
            Version = 0,
            At = DateTimeOffset.UtcNow,
            By = null,
            RawEvent = JObject.FromObject(new TestEvent(2), JsonSerializer.Create(PostgreSQLEventRepository.JsonSerializerSettings))
        };

        await baseEventRepository.ReplaceEvent(0, [newEvent]);

        // Verify the event was replaced
        var events = await baseEventRepository.GetEvents(-1, 0);
        Assert.Single(events);
        Assert.Equal(new TestEvent(2), events[0].Span[0].Event);
    }

    [Fact]
    public async Task TestReplaceEventWithMultiple()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();
        sc.AddPostgresEventSourcingRepository(_connectionString);

        var sp = sc.BuildServiceProvider();

        await sp.GetRequiredService<Migrator>().StartAsync(default);
        await sp.GetRequiredService<Upcaster>().StartAsync(default);

        await sp.GetRequiredService<Migrator>().WaitForFinish();

        var baseEventRepository = sp.GetRequiredService<PostgreSQLEventRepository>();

        // Publish an initial event
        await baseEventRepository.Publish([
            new EventEnvelope
            {
                Event = new TestEvent(1),
                Version = 0,
                At = DateTimeOffset.UtcNow,
                By = null
            }
        ]);

        // Replace the event with multiple events
        var newEvents = new List<RawEventEnvelope>
        {
            new RawEventEnvelope
            {
                Version = 0,
                At = DateTimeOffset.UtcNow,
                By = null,
                RawEvent = JObject.FromObject(new TestEvent(2), JsonSerializer.Create(PostgreSQLEventRepository.JsonSerializerSettings))
            },
            new RawEventEnvelope
            {
                Version = 1,
                At = DateTimeOffset.UtcNow,
                By = null,
                RawEvent = JObject.FromObject(new TestEvent(3), JsonSerializer.Create(PostgreSQLEventRepository.JsonSerializerSettings))
            }
        };

        await baseEventRepository.ReplaceEvent(0, newEvents);

        // Verify the events were replaced
        var events = await baseEventRepository.GetEvents(-1, 1);
        Assert.Equal(2, events[0].Length);
        Assert.Equal(new TestEvent(2), events[0].Span[0].Event);
        Assert.Equal(new TestEvent(3), events[0].Span[1].Event);
    }

    [Fact]
    public async Task TestUpcaster()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();
        sc.AddPostgresEventSourcingRepository(_connectionString);

        await using (var sp = sc.BuildServiceProvider())
        {
            var migrator = sp.GetRequiredService<Migrator>();

            await migrator.StartAsync(default);
            await migrator.WaitForFinish();

            var repository = sp.GetRequiredService<PostgreSQLEventRepository>();
            await repository.Publish([
                new EventEnvelope
                {
                    Event = new TestEvent(1),
                    Version = 0,
                    At = DateTimeOffset.UtcNow,
                    By = null
                }
            ]);
        }

        sc.AddUpcaster<TestEventUpcaster>();

        await using (var sp = sc.BuildServiceProvider())
        {
            var migrator = sp.GetRequiredService<Migrator>();
            var upcaster = sp.GetRequiredService<Upcaster>();

            await migrator.StartAsync(default);
            await migrator.WaitForFinish();
            await upcaster.StartAsync(default);
            await upcaster.ExecuteTask!;

            var repository = sp.GetRequiredService<PostgreSQLEventRepository>();
            var events = await repository.GetEvents(-1, 0);
            Assert.Single(events);
            Assert.Equal(new TestEvent2(1), events[0].Span[0].Event);
        }
    }

    [Fact]
    public async Task TestNewEventsSubscription()
    {
        var sc = new ServiceCollection();
        sc.AddEventSourcing();
        sc.AddPostgresEventSourcingRepository(_connectionString);

        await using var sp = sc.BuildServiceProvider();

        var migrator = sp.GetRequiredService<Migrator>();
        await migrator.StartAsync(default);
        await migrator.WaitForFinish();

        var repository = sp.GetRequiredService<PostgreSQLEventRepository>();

        var eventRaised = new TaskCompletionSource<bool>();

        void Handler(object? sender, EventArgs args)
        {
            try
            {
                eventRaised.SetResult(true);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        repository.NewEvents += Handler;

        try
        {
            await repository.Publish([
                new EventEnvelope
                {
                    Event = new TestEvent(1),
                    Version = 0,
                    At = DateTimeOffset.UtcNow,
                    By = null
                }
            ]);

            // Wait for the event to be raised or timeout after 5 seconds
            var eventRaisedTask = eventRaised.Task;
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

            var completedTask = await Task.WhenAny(eventRaisedTask, timeoutTask);

            Assert.Equal(eventRaisedTask, completedTask);
            Assert.True(await eventRaisedTask, "NewEvents event was not raised");
        }
        finally
        {
            repository.NewEvents -= Handler;
        }

        // Test removing the event handler
        var secondEventRaised = new TaskCompletionSource<bool>();
        repository.NewEvents += (_, _) =>
        {
            try
            {
                secondEventRaised.SetResult(true);
            }
            catch (Exception)
            {
                // ignored
            }
        };

        await repository.Publish([
            new EventEnvelope
            {
                Event = new TestEvent(2),
                Version = 1,
                At = DateTimeOffset.UtcNow,
                By = null
            }
        ]);

        var secondEventRaisedTask = secondEventRaised.Task;
        var secondTimeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

        var secondCompletedTask = await Task.WhenAny(secondEventRaisedTask, secondTimeoutTask);

        Assert.Equal(secondEventRaisedTask, secondCompletedTask);
        Assert.True(await secondEventRaisedTask, "NewEvents event was not raised after removing a handler");
    }

    [Fact]
    public async Task TestDatabaseNotificationForwarding()
    {
        var sc1 = new ServiceCollection();
        sc1.AddEventSourcing();
        sc1.AddPostgresEventSourcingRepository(_connectionString);

        var sc2 = new ServiceCollection();
        sc2.AddEventSourcing();
        sc2.AddPostgresEventSourcingRepository(_connectionString);

        await using var sp1 = sc1.BuildServiceProvider();
        await using var sp2 = sc2.BuildServiceProvider();

        var migrator = sp1.GetRequiredService<Migrator>();
        await migrator.StartAsync(default);
        await migrator.WaitForFinish();

        var repository1 = sp1.GetRequiredService<PostgreSQLEventRepository>();
        var repository2 = sp2.GetRequiredService<PostgreSQLEventRepository>();

        var eventRaised1 = new TaskCompletionSource<bool>();
        var eventRaised2 = new TaskCompletionSource<bool>();

        repository1.NewEvents += (_, _) =>
        {
            try
            {
                eventRaised1.TrySetResult(true);
            }
            catch (Exception)
            {
                // ignored
            }
        };
        repository2.NewEvents += (_, _) =>
        {
            try
            {
                eventRaised2.TrySetResult(true);
            }
            catch (Exception)
            {
                // ignored
            }
        };

        // Short delay to allow database trigger to register
        await Task.Delay(1000);

        // Publish an event using the first repository
        await repository1.Publish([
            new EventEnvelope
            {
                Event = new TestEvent(1),
                Version = 0,
                At = DateTimeOffset.UtcNow,
                By = null
            }
        ]);

        // Wait for both event handlers to be triggered or timeout after 5 seconds
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
        var fasterTask = await Task.WhenAny(
            Task.WhenAll(eventRaised1.Task, eventRaised2.Task),
            timeoutTask
        );

        Assert.True(timeoutTask != fasterTask, "Ran into timeout");
        Assert.True(await eventRaised1.Task, "NewEvents event was not raised on the first repository");
        Assert.True(await eventRaised2.Task, "NewEvents event was not raised on the second repository");
    }

    public async Task DisposeAsync()
    {
        await using var npgsqlConnection = new NpgsqlConnection(_managementConnectionString);
        await npgsqlConnection.OpenAsync();

        await using var command = new NpgsqlCommand($"DROP DATABASE {_dbName} WITH (FORCE)", npgsqlConnection);
        await command.ExecuteNonQueryAsync();
    }

    public record TestEvent(int Test) : Event;
    public record TestEvent2(int Test) : Event;

    public class TestEventUpcaster : Upcasting.Upcaster
    {
        public override IEnumerable<JObject>? Upcast(JObject eventJson)
        {
            var eventType = eventJson["$type"]?.Value<string>();
            if (eventType == typeof(TestEvent).AssemblyQualifiedName)
            {
                var eventJson2 = new JObject(eventJson)
                {
                    ["$type"] = typeof(TestEvent2).AssemblyQualifiedName
                };

                return
                [
                    eventJson2
                ];
            }

            return null;
        }
    }
}
