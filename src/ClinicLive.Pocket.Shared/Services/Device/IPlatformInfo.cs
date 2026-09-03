namespace ClinicLive.Pocket.Shared.Services.Device;

/// <summary>
/// The first "capability interface" — the pattern every native feature in this app follows.
///
/// The shared UI asks the question ("what am I running on?"); each host answers it with
/// what it actually has. MAUI answers from DeviceInfo; the web host answers "a browser".
/// Nothing in the shared project ever references a platform API directly.
/// </summary>
public interface IPlatformInfo
{
    /// <summary>"Android", "Windows", "iOS", "Web"…</summary>
    string Platform { get; }

    /// <summary>OS version string, for the About screen and bug reports.</summary>
    string Version { get; }

    /// <summary>Phone-sized screen — drives the "one hand, thumbs" layout choices.</summary>
    bool IsPhone { get; }

    /// <summary>"Native app" or "Browser" — which host is rendering these components.</summary>
    string Host { get; }
}
