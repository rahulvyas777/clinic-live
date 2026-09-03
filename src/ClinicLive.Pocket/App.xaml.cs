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

        // The shared code hears Resumed/Paused through IAppLifecycle; this is the
        // one place that knows they come from a MAUI Window.
        AppLifecycle.Instance.Attach(window);

        return window;
    }
}
