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
        // Register for notifications before the first window exists, as the docs
        // ask. Honest status (Part 5, unchanged through Part 11): this did NOT make
        // toasts appear. Registration succeeds, Setting == Enabled, Show() returns —
        // and no banner is displayed. The cause is app identity: this build is
        // unpackaged (no MSIX, no shortcut AUMID). See docs/pocket.md, "Windows —
        // self-contained, unpackaged".
        AppNotificationManager.Default.Register();

        base.OnLaunched(args);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
