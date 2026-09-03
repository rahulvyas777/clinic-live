using Android.App;
using Firebase.Messaging;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// Where Firebase delivers to the app. Two rules that surprise everyone the first time:
///
/// 1. When the app is in the BACKGROUND, a message with a "notification" payload is
///    shown by Android itself (on the channel the server named) and this method is
///    NOT called. It's only called for data-only messages, or in the foreground.
/// 2. When the app is in the FOREGROUND, Android shows nothing — it's on us. But
///    in the foreground the live queue (Part 4) and the nudges (Part 5) already
///    cover it, so we deliberately stay quiet here rather than notify twice.
/// </summary>
[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class PocketFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        // Tokens rotate. The Visit screen re-registers on every load (idempotent
        // upsert on the server), so the next time the app shows a visit the new
        // token replaces the old one. Nothing to do here but note it.
        base.OnNewToken(token);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        if (AppLifecycle.Instance.IsInForeground)
        {
            return;   // rule 2: the open screen handles it
        }

        // Data-only message in the background (the server always sends a notification
        // payload too, so this is belt-and-braces): show it ourselves.
        var title = message.Data.TryGetValue("title", out var t) ? t : "ClinicLive";
        var body = message.Data.TryGetValue("body", out var b) ? b : "Your visit has an update.";
        _ = new Notifier().ShowAsync(title, body);
    }
}
