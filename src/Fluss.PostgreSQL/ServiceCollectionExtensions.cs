using System.Reflection;
using FluentMigrator.Runner;
using Fluss.Core;
using Fluss.Events;
using Fluss.Upcasting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluss.PostgreSQL;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresEventSourcingRepository(this IServiceCollection services,
        string connectionString, Assembly? upcasterSourceAssembly = null)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (upcasterSourceAssembly is not null)
        {
            services
                .AddUpcasters(upcasterSourceAssembly)
                .AddHostedService<Upcaster>();
        }

        return services
            .AddScoped<IBaseEventRepository, Fluss.PostgreSQL.PostgreSQLEventRepository>()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(Fluss.PostgreSQL.PostgreSQLEventRepository).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .AddSingleton(new PostgreSQLConfig(connectionString))
            .AddSingleton<Migrator>()
            .AddHostedService(sp => sp.GetRequiredService<Migrator>());
    }
}

public class Migrator : BackgroundService
{
    private readonly ILogger<Migrator> _logger;
    private readonly IMigrationRunner _migrationRunner;
    private bool _didFinish;

    private readonly SemaphoreSlim _didFinishChanged = new(0, 1);

    public Migrator(IMigrationRunner migrationRunner, ILogger<Migrator> logger)
    {
        _migrationRunner = migrationRunner;
        _logger = logger;
    }

    public async Task WaitForFinish()
    {
        while (true)
        {
            if (_didFinish)
            {
                return;
            }
            await _didFinishChanged.WaitAsync();
            if (_didFinish)
            {
                _didFinishChanged.Release();
                return;
            }
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            try
            {
                Migrate();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while migrating");
            }
        }, stoppingToken);
    }

    public void Migrate()
    {
        //_migrationRunner.ListMigrations();
        try
        {
            _migrationRunner.MigrateUp();
        }
        catch
        {
            Environment.Exit(-1);
        }

        _didFinish = true;
        _didFinishChanged.Release();
    }
}

public class Upcaster : BackgroundService
{
    private readonly EventUpcasterService _upcasterService;
    private readonly Migrator _migrator;
    private readonly ILogger<Upcaster> _logger;

    public Upcaster(EventUpcasterService upcasterService, Migrator migrator, ILogger<Upcaster> logger)
    {
        _upcasterService = upcasterService;
        _migrator = migrator;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            _logger.LogInformation("Waiting for migration to finish");
            await _migrator.WaitForFinish();
            _logger.LogInformation("Migration finished, starting event upcasting");

            try
            {
                await _upcasterService.Run();
                _logger.LogInformation("Event upcasting finished");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while upcasting");
                throw;
            }
        }, stoppingToken);
    }
}

public class PostgreSQLConfig
{
    public PostgreSQLConfig(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }
}
