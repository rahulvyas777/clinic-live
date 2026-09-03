namespace ClinicLive.Pocket.Services;

/// <summary>Mac Catalyst: same story as iOS — not built here, honest no-op.</summary>
public sealed partial class Notifier
{
    public partial Task<bool> RequestPermissionAsync() => Task.FromResult(false);

    public partial Task ShowAsync(string title, string body) => Task.CompletedTask;
}
