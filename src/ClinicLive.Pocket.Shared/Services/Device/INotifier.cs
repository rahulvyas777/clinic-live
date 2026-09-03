namespace ClinicLive.Pocket.Shared.Services.Device;

/// <summary>
/// Capability #4: a notification the user sees even when the app isn't in front —
/// the system tray on Android, a toast on Windows, the Notification API in a browser.
/// Asking permission is a separate step on purpose: ask at a moment that explains
/// itself ("we'll tell you when it's your turn"), never at launch.
/// </summary>
public interface INotifier
{
    Task<bool> RequestPermissionAsync();

    Task ShowAsync(string title, string body);
}
