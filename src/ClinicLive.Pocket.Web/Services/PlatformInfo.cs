using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Web.Services;

/// <summary>The browser's answer. It renders on the server, so "version" is the server's — say so.</summary>
public sealed class PlatformInfo : IPlatformInfo
{
    public string Platform => "Web";

    public string Version => $"(server: {Environment.OSVersion.Platform})";

    // Server-side rendering can't see the screen; the CSS decides the layout instead.
    public bool IsPhone => false;

    public string Host => "Browser";
}
