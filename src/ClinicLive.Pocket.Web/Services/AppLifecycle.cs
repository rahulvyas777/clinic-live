using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Web.Services;

/// <summary>
/// The browser's answer: a server-rendered page is never "paused" from the server's
/// point of view — the circuit either exists or it doesn't. Honest no-op.
/// </summary>
public sealed class AppLifecycle : IAppLifecycle
{
    public event Action? Resumed { add { } remove { } }
    public event Action? Paused { add { } remove { } }
}
