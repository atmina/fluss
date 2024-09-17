using System.Reflection;
using FluentMigrator.Runner;
using Fluss.Upcasting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluss.PostgreSQL;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresEventSourcingRepository(this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddBaseEventRepository<PostgreSQLEventRepository>()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(PostgreSQLEventRepository).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .AddSingleton(new PostgreSQLConfig(connectionString))
            .AddSingleton<Migrator>()
            .AddHostedService(sp => sp.GetRequiredService<Migrator>())
            .AddHostedService<Upcaster>();
    }
}

public class Migrator(ILogger<Migrator> logger, IServiceProvider serviceProvider) : BackgroundService
{
    private bool _didFinish;

    private readonly SemaphoreSlim _didFinishChanged = new(0, 1);

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
                var scope = serviceProvider.CreateScope();
                var migrationRunner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                migrationRunner.MigrateUp();

                _didFinish = true;
                _didFinishChanged.Release();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while migrating");
                Environment.Exit(-1);
            }
        }, stoppingToken);
    }
}

public class Upcaster(EventUpcasterService upcasterService, Migrator migrator, ILogger<Upcaster> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            logger.LogInformation("Waiting for migration to finish");
            await migrator.WaitForFinish();
            logger.LogInformation("Migration finished, starting event upcasting");

            try
            {
                await upcasterService.Run();
                logger.LogInformation("Event upcasting finished");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while upcasting");
                throw;
            }
        }, stoppingToken);
    }
}

public class PostgreSQLConfig(string connectionString)
{
    public string ConnectionString { get; } = connectionString;
}
