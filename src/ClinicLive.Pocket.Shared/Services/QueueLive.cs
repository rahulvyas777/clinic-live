using ClinicLive.Pocket.Shared.Services.Device;
using Microsoft.AspNetCore.SignalR.Client;

namespace ClinicLive.Pocket.Shared.Services;

/// <summary>
/// The app's one SignalR connection to the clinic's QueueHub — the same thin hub the
/// waiting-room TV listens to. It carries a single signal, "QueueChanged", and the app
/// re-asks the API for fresh state. Notify, don't ship state (Season 1, Part 7).
///
/// One instance per app, started on demand, kept alive across screens, and nudged by
/// the host's lifecycle: a phone that comes back from the background has missed
/// events, so Resumed always means "reconnect and re-read".
/// </summary>
public sealed class QueueLive : IAsyncDisposable
{
    private readonly HubConnection _hub;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;

    public QueueLive(ClinicEndpoint endpoint, IAppLifecycle lifecycle)
    {
        // NOT the default policy. WithAutomaticReconnect() tries at 0, 2, 10 and 30
        // seconds and then gives up forever — fine for a browser tab someone will
        // refresh, wrong for a phone in a car park. The screenshot that proved it:
        // server back, pill still saying "reconnecting…". Keep trying, with backoff.
        _hub = new HubConnectionBuilder()
            .WithUrl(endpoint.QueueHub)
            .WithAutomaticReconnect(new KeepTrying())
            .Build();

        _hub.On("QueueChanged", () => Changed?.Invoke());

        _hub.Reconnecting += _ =>
        {
            ConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };
        _hub.Reconnected += async _ =>
        {
            await _hub.InvokeAsync("JoinBoard");   // groups don't survive a reconnect
            ConnectionChanged?.Invoke(true);
            Changed?.Invoke();                     // catch up on whatever we missed
        };
        _hub.Closed += _ =>
        {
            ConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        lifecycle.Resumed += () => _ = EnsureStartedAsync(catchUp: true);
    }

    /// <summary>Raised on a background thread whenever the clinic's queue changes.</summary>
    public event Action? Changed;

    /// <summary>true = connected; false = reconnecting or closed.</summary>
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _hub.State == HubConnectionState.Connected;

    /// <summary>Idempotent: the first screen that needs live data starts the connection; later ones reuse it.</summary>
    public async Task EnsureStartedAsync(bool catchUp = false)
    {
        await _gate.WaitAsync();
        try
        {
            if (_hub.State == HubConnectionState.Disconnected)
            {
                await _hub.StartAsync();
                await _hub.InvokeAsync("JoinBoard");
                _started = true;
                ConnectionChanged?.Invoke(true);
                catchUp = true;
            }
        }
        catch (Exception)
        {
            // The clinic is down or unreachable. WithAutomaticReconnect only kicks in
            // after a successful start, so the next Resumed (or screen) tries again.
            ConnectionChanged?.Invoke(false);
            return;
        }
        finally
        {
            _gate.Release();
        }

        if (catchUp && _started)
        {
            Changed?.Invoke();
        }
    }

    public ValueTask DisposeAsync() => _hub.DisposeAsync();

    /// <summary>2s, 4s, 8s, 16s, then every 30s — and never returns null, so it never stops.</summary>
    private sealed class KeepTrying : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, retryContext.PreviousRetryCount + 1)));
    }
}
