using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// Windows: a toast through the Windows App SDK. Registration happens at startup in
/// Platforms/Windows/App.xaml.cs (unpackaged apps must register before the window
/// exists). There's no permission dialog — the user controls it from
/// Settings › Notifications, and Setting tells us what they chose.
/// </summary>
public sealed partial class Notifier
{
    public partial Task<bool> RequestPermissionAsync() =>
        Task.FromResult(AppNotificationManager.Default.Setting == AppNotificationSetting.Enabled);

    public partial Task ShowAsync(string title, string body)
    {
        var toast = new AppNotificationBuilder()
            .AddText(title)
            .AddText(body)
            .BuildNotification();
        AppNotificationManager.Default.Show(toast);
        return Task.CompletedTask;
    }
}
