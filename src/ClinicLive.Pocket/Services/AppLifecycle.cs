using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// MAUI's answer: the Window's Resumed/Stopped events. One instance for the whole
/// process — App.CreateWindow feeds it, DI hands it to the shared code.
/// </summary>
public sealed class AppLifecycle : IAppLifecycle
{
    public static AppLifecycle Instance { get; } = new();

    public event Action? Resumed;
    public event Action? Paused;

    public void Attach(Window window)
    {
        window.Resumed += (_, _) => Resumed?.Invoke();
        window.Stopped += (_, _) => Paused?.Invoke();
    }
}
