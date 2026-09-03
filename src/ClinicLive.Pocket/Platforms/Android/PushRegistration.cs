using Android.Gms.Extensions;
using Firebase.Messaging;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// Android: ask Firebase for this install's token. Firebase initialises itself from
/// google-services.json (the GoogleServicesJson build action turns it into resources),
/// so there's no setup call — just a token request that may take a network round-trip.
/// </summary>
public sealed partial class PushRegistration
{
    public partial string? Platform => "android";

    public partial async Task<string?> GetTokenAsync()
    {
        try
        {
            var token = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
            return token?.ToString();
        }
        catch (Exception)
        {
            // No Play services, no network, no google-services.json: the visit screen
            // simply won't promise push. Nothing else breaks.
            return null;
        }
    }
}
