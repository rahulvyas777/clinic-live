namespace ClinicLive.Pocket.Services;

/// <summary>Mac Catalyst: same story as iOS. Honest null.</summary>
public sealed partial class PushRegistration
{
    public partial string? Platform => null;

    public partial Task<string?> GetTokenAsync() => Task.FromResult<string?>(null);
}
