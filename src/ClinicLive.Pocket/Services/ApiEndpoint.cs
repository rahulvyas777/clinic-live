namespace ClinicLive.Pocket.Services;

/// <summary>
/// Where the clinic's server is, from THIS device's point of view.
///
/// "localhost" inside the Android emulator is the emulator itself; the machine running
/// it is 10.0.2.2. A physical phone on your Wi-Fi needs the PC's LAN address instead
/// (and the server listening on 0.0.0.0, and a firewall rule) — Part 9 makes this a setting.
/// </summary>
public static class ApiEndpoint
{
    public const int Port = 5159;   // ClinicLive's http launch profile

    public static Uri Base =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? new Uri($"http://10.0.2.2:{Port}/")
            : new Uri($"http://localhost:{Port}/");
}
