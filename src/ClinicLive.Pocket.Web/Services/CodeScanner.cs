using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Web.Services;

/// <summary>
/// The browser's answer: not here. (getUserMedia + a JS decoder is a real option
/// for a phone browser; this series keeps the camera for the native app and lets
/// the web visitor type six characters.)
/// </summary>
public sealed class CodeScanner : ICodeScanner
{
    public bool IsSupported => false;

    public Task<string?> ScanAsync() => Task.FromResult<string?>(null);
}
