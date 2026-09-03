using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// MAUI's answer: Preferences (a plain key-value file) for the harmless things, and
/// SecureStorage (Android Keystore / Windows DPAPI / iOS Keychain) for the one value
/// that lets anyone act as the patient — the confirmation code.
/// </summary>
public sealed class AppStorage : IAppStorage
{
    public ValueTask<string?> GetAsync(string key) =>
        new(Preferences.Default.ContainsKey(key) ? Preferences.Default.Get<string?>(key, null) : null);

    public ValueTask SetAsync(string key, string? value)
    {
        if (value is null)
        {
            Preferences.Default.Remove(key);
        }
        else
        {
            Preferences.Default.Set(key, value);
        }
        return ValueTask.CompletedTask;
    }

    public async ValueTask<string?> GetSecureAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception)
        {
            // A reset keystore (device restore, cleared credentials) throws on read.
            // Treat it as "nothing remembered" rather than crashing the Home screen.
            return null;
        }
    }

    public async ValueTask SetSecureAsync(string key, string? value)
    {
        if (value is null)
        {
            SecureStorage.Default.Remove(key);
        }
        else
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
    }
}
