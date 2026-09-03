using ClinicLive.Pocket.Services;

namespace ClinicLive.Pocket;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "ClinicLive Pocket" };

        // Desktop only (ignored on phones): open at a reception-desk size, never
        // shrink below a phone's worth of width. Part 2's first Windows screenshot was
        // a phone layout stretched across a 4K monitor — this is the fix.
        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
        {
            window.Width = 1100;
            window.Height = 760;
            window.MinimumWidth = 420;
            window.MinimumHeight = 640;
        }

        // The shared code hears Resumed/Paused through IAppLifecycle; this is the
        // one place that knows they come from a MAUI Window.
        AppLifecycle.Instance.Attach(window);

        return window;
    }
}
