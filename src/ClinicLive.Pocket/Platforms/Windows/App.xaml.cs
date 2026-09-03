using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;

namespace ClinicLive.Pocket.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Unpackaged apps must register for notifications BEFORE the first window
        // exists — registering lazily "works" (no exception, Setting == Enabled) and
        // then Show() quietly displays nothing. Part 5's screenshot proved it.
        AppNotificationManager.Default.Register();

        base.OnLaunched(args);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
