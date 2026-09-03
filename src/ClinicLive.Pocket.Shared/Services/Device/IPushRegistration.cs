namespace ClinicLive.Pocket.Shared.Services.Device;

/// <summary>
/// Capability #5: "how does the clinic reach this device when the app is closed?"
/// Android answers with a Firebase token. Windows, the web and (here) iOS answer
/// null — and the shared UI simply doesn't promise what it can't deliver.
/// </summary>
public interface IPushRegistration
{
    /// <summary>The platform name the server stores ("android"), or null when push isn't available.</summary>
    string? Platform { get; }

    /// <summary>The device's push token, or null when push isn't available on this host.</summary>
    Task<string?> GetTokenAsync();
}
