using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>MAUI's answer: Connectivity from Essentials, which also raises an event when the bars change.</summary>
public sealed class ConnectivityInfo : IConnectivityInfo
{
    public ConnectivityInfo()
    {
        Connectivity.Current.ConnectivityChanged += (_, e) =>
            Changed?.Invoke(e.NetworkAccess == NetworkAccess.Internet);
    }

    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public event Action<bool>? Changed;

    public ValueTask EnsureStartedAsync() => ValueTask.CompletedTask;
}
