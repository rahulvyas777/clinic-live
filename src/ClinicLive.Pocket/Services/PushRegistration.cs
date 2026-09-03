using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>Push tokens are per platform, so the bodies live in Platforms/*/PushRegistration.cs.</summary>
public sealed partial class PushRegistration : IPushRegistration
{
    public partial string? Platform { get; }

    public partial Task<string?> GetTokenAsync();
}
