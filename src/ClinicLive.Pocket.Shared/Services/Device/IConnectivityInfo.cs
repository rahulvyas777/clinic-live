namespace ClinicLive.Pocket.Shared.Services.Device;

/// <summary>
/// Capability #8: "can this device reach the internet right now?" A car park has
/// one bar; a basement has none. The answer changes while the app is open.
/// </summary>
public interface IConnectivityInfo
{
    bool IsOnline { get; }

    event Action<bool>? Changed;

    /// <summary>
    /// Hosts that need a browser to answer (the web host asks navigator.onLine over JS
    /// interop) can only start listening after the first render. Native hosts no-op.
    /// </summary>
    ValueTask EnsureStartedAsync();
}
