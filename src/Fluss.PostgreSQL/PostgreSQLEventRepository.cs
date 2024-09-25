using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using Fluss.Events;
using Fluss.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;

namespace Fluss.PostgreSQL;

public partial class PostgreSQLEventRepository : IBaseEventRepository
{
    private readonly NpgsqlDataSource dataSource;

    public PostgreSQLEventRepository(PostgreSQLConfig config)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(config.ConnectionString);
        dataSourceBuilder.UseJsonNet(settings: new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            MetadataPropertyHandling =
                    MetadataPropertyHandling.ReadAhead // While this is marked as a performance hit, profiling approves
        });
        dataSource = dataSourceBuilder.Build();
    }

    private async ValueTask Publish<TEnvelope>(IReadOnlyList<TEnvelope> envelopes, Func<TEnvelope, object> eventExtractor,
        NpgsqlConnection? conn = null) where TEnvelope : Envelope
    {
        using var activity = ActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.EventRepository", nameof(PostgreSQLEventRepository));

        // await using var connection has the side-effect that our connection passed from the outside is also disposed, so we split this up.
        await using var freshConnection = dataSource.OpenConnection();
        var connection = conn ?? freshConnection;

        activity?.AddEvent(new ActivityEvent("Connection open"));

        await using var writer =
            connection.BeginBinaryImport(
                """COPY "Events" ("Version", "At", "By", "Event") FROM STDIN (FORMAT BINARY)""");

        activity?.AddEvent(new ActivityEvent("Got Writer"));

        try
        {
            foreach (var eventEnvelope in envelopes.OrderBy(e => e.Version))
            {
                // ReSharper disable MethodHasAsyncOverload
                writer.StartRow();
                writer.Write(eventEnvelope.Version);
                writer.Write(eventEnvelope.At);
                if (eventEnvelope.By != null)
                {
                    writer.Write(eventEnvelope.By.Value);
                }
                else
                {
                    writer.Write(DBNull.Value);
                }

                writer.Write(eventExtractor(eventEnvelope), NpgsqlDbType.Jsonb);
                // ReSharper enable MethodHasAsyncOverload
            }

            await writer.CompleteAsync();
        }
        catch (PostgresException e)
        {
            if (e is { SqlState: "23505", TableName: "Events" })
            {
                throw new RetryException();
            }

            throw;
        }

        NotifyNewEvents();
    }

    public async ValueTask Publish(IReadOnlyList<EventEnvelope> envelopes)
    {
        await Publish(envelopes, e => e.Event);
    }

    private async ValueTask<TResult> WithReader<TResult>(long fromExclusive, long toInclusive,
        Func<NpgsqlDataReader, ValueTask<TResult>> action)
    {
        await using var connection = dataSource.OpenConnection();
        await using var cmd =
            new NpgsqlCommand(
                """
                SELECT "Version", "At", "By", "Event" FROM "Events" WHERE "Version" > @from AND "Version" <= @to ORDER BY "Version"
                """,
                connection);

        cmd.Parameters.AddWithValue("@from", fromExclusive);
        cmd.Parameters.AddWithValue("@to", toInclusive);
        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

        var result = await action(reader);

        await reader.CloseAsync();
        return result;
    }

    public async ValueTask<ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>>> GetEvents(long fromExclusive,
        long toInclusive)
    {
        using var activity = ActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.EventRepository", nameof(PostgreSQLEventRepository));
        activity?.SetTag("EventSourcing.EventRequest", $"{fromExclusive}-{toInclusive}");

        return await WithReader(fromExclusive, toInclusive, async reader =>
        {
            var envelopes = new List<EventEnvelope>();

            while (await reader.ReadAsync())
            {
                envelopes.Add(new EventEnvelope
                {
                    Version = reader.GetInt64(0),
                    At = reader.GetDateTime(1),
                    By = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                    Event = reader.GetFieldValue<Event>(3),
                });
            }

            return envelopes.ToPagedMemory();
        });
    }

    public async ValueTask<IEnumerable<RawEventEnvelope>> GetRawEvents()
    {
        var latestVersion = await GetLatestVersion();
        return await WithReader(-1, latestVersion, async reader =>
        {
            var envelopes = new List<RawEventEnvelope>();

            while (await reader.ReadAsync())
            {
                envelopes.Add(new RawEventEnvelope
                {
                    Version = reader.GetInt64(0),
                    At = reader.GetDateTime(1),
                    By = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                    RawEvent = reader.GetFieldValue<JObject>(3),
                });
            }

            return envelopes;
        });
    }

    public async ValueTask ReplaceEvent(long at, IEnumerable<RawEventEnvelope> newEnvelopes)
    {
        var envelopes = newEnvelopes.ToList();

        await using var connection = dataSource.OpenConnection();
        await using var transaction = connection.BeginTransaction();

        await using var deleteCommand =
            new NpgsqlCommand("""DELETE FROM "Events" WHERE "Version" = @at;""", connection);
        deleteCommand.Parameters.AddWithValue("at", at);
        await deleteCommand.ExecuteNonQueryAsync();

        if (envelopes.Count != 1)
        {
            // Deferring constraints to allow updating the primary key and shifting the versions
            await using var deferConstraintsCommand =
                new NpgsqlCommand("""SET CONSTRAINTS "PK_Events" DEFERRED;""", connection);
            await deferConstraintsCommand.ExecuteNonQueryAsync();

            await using var versionUpdateCommand =
                new NpgsqlCommand(
                    """UPDATE "Events" e SET "Version" = e."Version" + @offset WHERE e."Version" > @at;""",
                    connection);

            versionUpdateCommand.Parameters.AddWithValue("offset", envelopes.Count - 1);
            versionUpdateCommand.Parameters.AddWithValue("at", at);
            await versionUpdateCommand.ExecuteNonQueryAsync();
        }

        await Publish(envelopes, e => e.RawEvent, connection);

        await transaction.CommitAsync();
    }

    private Task<long>? _latestVersionTask;
    private readonly object _latestVersionLock = new();
    public async ValueTask<long> GetLatestVersion()
    {
        using var activity = ActivitySource.Source.StartActivity();
        activity?.SetTag("EventSourcing.EventRepository", nameof(PostgreSQLEventRepository));

        var task = _latestVersionTask;

        if (task == null)
        {
            lock (_latestVersionLock)
            {
                task = _latestVersionTask;
                if (task == null)
                {
                    task = GetLatestVersionImpl();
                    _latestVersionTask = task;
                }
            }
        }

        var num = await task;

        if (_latestVersionTask == task)
        {
            lock (_latestVersionLock)
            {
                if (_latestVersionTask == task)
                {
                    _latestVersionTask = null;
                }
            }
        }

        return num;
    }

    private async Task<long> GetLatestVersionImpl()
    {
        await using var connection = dataSource.OpenConnection();

        await using var cmd = new NpgsqlCommand("""SELECT MAX("Version") FROM "Events";""", connection);
        await cmd.PrepareAsync();
        var scalar = await cmd.ExecuteScalarAsync();

        if (scalar is DBNull)
        {
            return -1;
        }

        return (long)(scalar ?? -1);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_cancellationTokenSource.IsCancellationRequested) return;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
