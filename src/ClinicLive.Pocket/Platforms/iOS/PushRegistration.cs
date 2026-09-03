namespace ClinicLive.Pocket.Services;

/// <summary>iOS: APNs needs a Mac, an Apple developer account and a certificate — none on this bench. Honest null.</summary>
public sealed partial class PushRegistration
{
    public partial string? Platform => null;

    public partial Task<string?> GetTokenAsync() => Task.FromResult<string?>(null);
}
