using Npgsql;

namespace Fluss.PostgreSQL;

public partial class PostgreSQLEventRepository : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private EventHandler? _newEvents;

    private bool _triggerInitialized;

    public event EventHandler NewEvents
    {
        add
        {
            _newEvents += value;
#pragma warning disable 4014
            InitializeTrigger();
#pragma warning restore 4014
        }

        remove
        {
            _newEvents -= value;
        }
    }

    private async Task InitializeTrigger()
    {
        if (_triggerInitialized)
        {
            return;
        }

        _triggerInitialized = true;
        await using var listenConnection = dataSource.CreateConnection();
        await listenConnection.OpenAsync(_cancellationTokenSource.Token);

        listenConnection.Notification += (_, _) =>
        {
            NotifyNewEvents();
        };

        await using var listen = new NpgsqlCommand("LISTEN new_event", listenConnection);
        await listen.ExecuteNonQueryAsync(_cancellationTokenSource.Token);

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            await listenConnection.WaitAsync(_cancellationTokenSource.Token);
        }

        await using var unlisten = new NpgsqlCommand("UNLISTEN new_event", listenConnection);
        await unlisten.ExecuteNonQueryAsync(new CancellationToken());
    }

    private async void NotifyNewEvents()
    {
        await Task.Run(() =>
        {
            _newEvents?.Invoke(this, EventArgs.Empty);
        });
    }
}
