using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>MAUI's answer: ask the device.</summary>
public sealed class PlatformInfo : IPlatformInfo
{
    public string Platform => DeviceInfo.Platform.ToString();

    public string Version => DeviceInfo.VersionString;

    public bool IsPhone => DeviceInfo.Idiom == DeviceIdiom.Phone;

    public string Host => "Native app";
}
