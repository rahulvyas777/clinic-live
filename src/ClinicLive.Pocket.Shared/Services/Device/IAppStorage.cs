namespace ClinicLive.Pocket.Shared.Services.Device;

/// <summary>
/// Capability #9: remember things between launches. Two tiers on purpose —
/// Preferences for harmless settings and caches, SecureStorage for the one thing
/// that IS a credential here: the confirmation code. On Android that's the
/// Keystore-backed store; in a browser it's localStorage with an honest caveat.
/// </summary>
public interface IAppStorage
{
    ValueTask<string?> GetAsync(string key);

    ValueTask SetAsync(string key, string? value);

    ValueTask<string?> GetSecureAsync(string key);

    ValueTask SetSecureAsync(string key, string? value);
}
