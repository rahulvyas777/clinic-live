namespace ClinicLive.Pocket.Services;

/// <summary>
/// Where the clinic's server is, from THIS device's point of view.
///
/// "localhost" inside the Android emulator is the emulator itself; the machine running
/// it is 10.0.2.2. A physical phone on your Wi-Fi needs the PC's LAN address instead
/// (and the server listening on 0.0.0.0, and a firewall rule) — Part 9's Settings
/// screen stores an override in Preferences, read here at startup.
/// </summary>
public static class ApiEndpoint
{
    public const int Port = 5159;   // ClinicLive's http launch profile

    public static Uri Default =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? new Uri($"http://10.0.2.2:{Port}/")
            : new Uri($"http://localhost:{Port}/");

    public static Uri Base
    {
        get
        {
            var saved = Preferences.Default.Get<string?>("api.base", null);
            return !string.IsNullOrWhiteSpace(saved) && Uri.TryCreate(saved, UriKind.Absolute, out var uri)
                ? uri
                : Default;
        }
    }
}
