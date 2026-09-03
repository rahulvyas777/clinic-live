using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace ClinicLive.Services;

/// <summary>Sends a push notification to a set of device tokens. One method; the rest is plumbing.</summary>
public interface IPushSender
{
    Task SendAsync(IReadOnlyList<string> tokens, string title, string body, IReadOnlyDictionary<string, string> data);
}

/// <summary>
/// The feature flag in class form: when Push:ServiceAccountPath isn't configured
/// (tests, a fresh clone, CI), pushes are logged and nothing leaves the building.
/// </summary>
public sealed class NullPushSender(ILogger<NullPushSender> logger) : IPushSender
{
    public Task SendAsync(IReadOnlyList<string> tokens, string title, string body, IReadOnlyDictionary<string, string> data)
    {
        logger.LogInformation("Push not configured — would have sent \"{Title}\" to {Count} device(s)", title, tokens.Count);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Firebase Cloud Messaging through the Admin SDK. The service-account JSON is the
/// clinic's credential to talk to Firebase — it lives outside the repo and is read
/// once at startup. Sends carry BOTH a notification (so Android's tray shows it when
/// the app is closed) and data (so the app can act on it when it's open).
/// </summary>
public sealed class FcmPushSender : IPushSender
{
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(string serviceAccountPath, ILogger<FcmPushSender> logger)
    {
        _logger = logger;
        if (FirebaseApp.DefaultInstance is null)
        {
            FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(serviceAccountPath) });
        }
    }

    public async Task SendAsync(IReadOnlyList<string> tokens, string title, string body, IReadOnlyDictionary<string, string> data)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        var message = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new Notification { Title = title, Body = body },
            Data = new Dictionary<string, string>(data),
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification { ChannelId = "queue" },
            },
        };

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
        _logger.LogInformation("Push \"{Title}\": {Success} delivered, {Failure} failed", title, response.SuccessCount, response.FailureCount);

        foreach (var failure in response.Responses.Where(r => !r.IsSuccess))
        {
            _logger.LogWarning("Push failure: {Reason}", failure.Exception?.Message);
        }
    }
}
