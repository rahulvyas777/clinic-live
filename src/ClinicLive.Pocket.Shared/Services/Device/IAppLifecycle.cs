namespace ClinicLive.Pocket.Shared.Services.Device;

/// <summary>
/// Capability #2: "is the app in front of the user?" A phone app is paused, frozen and
/// resumed all day; a server-rendered web page simply isn't. Sockets die in the
/// background, so anything live must re-sync on Resumed.
/// </summary>
public interface IAppLifecycle
{
    /// <summary>The user came back to the app (or it started).</summary>
    event Action? Resumed;

    /// <summary>The app went to the background.</summary>
    event Action? Paused;
}
