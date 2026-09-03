using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// Notifications have no cross-platform API in MAUI, so this is a PARTIAL class: the
/// shape lives here, the body lives in Platforms/{Android,Windows,...}/Notifier.cs,
/// and only the file for the platform being built is compiled. The MAUI-idiomatic
/// way to say "this is different on every OS".
/// </summary>
public sealed partial class Notifier : INotifier
{
    public partial Task<bool> RequestPermissionAsync();

    public partial Task ShowAsync(string title, string body);
}
