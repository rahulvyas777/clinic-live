using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// Android: a notification channel (mandatory since 8.0), the runtime POST_NOTIFICATIONS
/// permission (mandatory since 13), and NotificationCompat to build the thing. About
/// forty lines, no plugin — worth seeing once in full.
/// </summary>
public sealed partial class Notifier
{
    public const string ChannelId = "queue";
    private static int _nextId;

    /// <summary>
    /// Idempotent. Called at startup too (MainActivity), because a push delivered while
    /// the app is closed is shown by Android on THIS channel — it has to exist already.
    /// </summary>
    public static void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }
        var context = Android.App.Application.Context;
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Queue updates", NotificationImportance.High)
        {
            Description = "When you're next, and when it's your turn",
        });
    }

    public partial async Task<bool> RequestPermissionAsync()
    {
        // Pre-13 devices grant this implicitly; MAUI's Permissions handles the version check.
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        return status == PermissionStatus.Granted;
    }

    public partial Task ShowAsync(string title, string body)
    {
        EnsureChannel();
        var context = Android.App.Application.Context;
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;

        // Tapping the notification brings the app to the front.
        var launch = context.PackageManager!.GetLaunchIntentForPackage(context.PackageName!);
        var tap = PendingIntent.GetActivity(context, 0, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // The app icon MAUI generated for us, looked up by name so no resource designer is involved.
        var icon = context.Resources!.GetIdentifier("appicon", "mipmap", context.PackageName);

        var notification = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(icon)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true)
            .SetContentIntent(tap)
            .Build();

        manager.Notify(Interlocked.Increment(ref _nextId), notification);
        return Task.CompletedTask;
    }
}
