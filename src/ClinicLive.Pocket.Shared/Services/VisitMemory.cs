using System.Text.Json;
using ClinicLive.Contracts;
using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Shared.Services;

/// <summary>
/// What the app remembers about your visit when you close it: the code (securely)
/// and the last thing the clinic said (as a cache with a timestamp), so a phone with
/// no signal in the waiting room still shows something true — clearly labelled as old.
/// </summary>
public sealed class VisitMemory(IAppStorage storage)
{
    private const string CodeKey = "visit.code";
    private const string CacheKey = "visit.last";
    private const string NudgesKey = "nudges.enabled";

    public sealed record Cached(VisitDto Visit, DateTime StoredAtUtc);

    public ValueTask<string?> GetCodeAsync() => storage.GetSecureAsync(CodeKey);

    public async ValueTask RememberAsync(VisitDto visit)
    {
        await storage.SetSecureAsync(CodeKey, visit.Code);
        await storage.SetAsync(CacheKey, JsonSerializer.Serialize(new Cached(visit, DateTime.UtcNow)));
    }

    public async ValueTask<Cached?> GetCachedAsync()
    {
        var json = await storage.GetAsync(CacheKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<Cached>(json);
        }
        catch (JsonException)
        {
            return null;   // an older app version wrote a shape we no longer read
        }
    }

    public async ValueTask ForgetAsync()
    {
        await storage.SetSecureAsync(CodeKey, null);
        await storage.SetAsync(CacheKey, null);
    }

    public async ValueTask<bool> NudgesEnabledAsync() =>
        await storage.GetAsync(NudgesKey) is not "false";

    public ValueTask SetNudgesEnabledAsync(bool enabled) =>
        storage.SetAsync(NudgesKey, enabled ? "true" : "false");
}
