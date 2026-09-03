namespace ClinicLive.Pocket.Services;

/// <summary>
/// iOS is not built in this series (no Mac on the bench), so this stays honest: it
/// compiles and it says no. The real answer is UNUserNotificationCenter — request
/// authorization, then add a UNNotificationRequest — and it belongs in this file.
/// </summary>
public sealed partial class Notifier
{
    public partial Task<bool> RequestPermissionAsync() => Task.FromResult(false);

    public partial Task ShowAsync(string title, string body) => Task.CompletedTask;
}
