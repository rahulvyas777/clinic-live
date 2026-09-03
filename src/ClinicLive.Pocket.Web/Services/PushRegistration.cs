using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Web.Services;

/// <summary>
/// The browser's answer: Web Push exists (service worker + VAPID keys) but is a
/// different series. Honest null — the page keeps working live while it's open.
/// </summary>
public sealed class PushRegistration : IPushRegistration
{
    public string? Platform => null;

    public Task<string?> GetTokenAsync() => Task.FromResult<string?>(null);
}
