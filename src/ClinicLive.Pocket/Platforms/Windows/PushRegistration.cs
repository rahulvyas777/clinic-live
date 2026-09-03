namespace ClinicLive.Pocket.Services;

/// <summary>Windows: no push in this series (WNS needs a Store identity). Honest null.</summary>
public sealed partial class PushRegistration
{
    public partial string? Platform => null;

    public partial Task<string?> GetTokenAsync() => Task.FromResult<string?>(null);
}
