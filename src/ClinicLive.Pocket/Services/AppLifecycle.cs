using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// MAUI's answer: the Window's Resumed/Stopped events. One instance for the whole
/// process — App.CreateWindow feeds it, DI hands it to the shared code, and the
/// Firebase service (Part 6) asks it whether the app is in front.
/// </summary>
public sealed class AppLifecycle : IAppLifecycle
{
    public static AppLifecycle Instance { get; } = new();

    public event Action? Resumed;
    public event Action? Paused;

    // false until a window actually resumes: a process that Firebase starts to
    // deliver a message has NO window, and must not think it's in front.
    public bool IsInForeground { get; private set; }

    public void Attach(Window window)
    {
        window.Resumed += (_, _) =>
        {
            IsInForeground = true;
            Resumed?.Invoke();
        };
        window.Stopped += (_, _) =>
        {
            IsInForeground = false;
            Paused?.Invoke();
        };
    }
}
