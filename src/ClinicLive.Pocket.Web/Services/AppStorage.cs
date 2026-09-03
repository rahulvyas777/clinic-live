using ClinicLive.Pocket.Shared.Services.Device;
using Microsoft.JSInterop;

namespace ClinicLive.Pocket.Web.Services;

/// <summary>
/// The browser's answer: localStorage for both tiers — a browser has no keystore the
/// page can use, and the UI says so. During prerendering there is no browser yet,
/// so reads answer "nothing" and the interactive render asks again.
/// </summary>
public sealed class AppStorage(IJSRuntime js) : IAppStorage
{
    public async ValueTask<string?> GetAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("pocketDevice.storageGet", key);
        }
        catch (InvalidOperationException)
        {
            return null;   // prerender: JS interop isn't available yet
        }
    }

    public async ValueTask SetAsync(string key, string? value)
    {
        try
        {
            await js.InvokeVoidAsync("pocketDevice.storageSet", key, value);
        }
        catch (InvalidOperationException)
        {
        }
    }

    public ValueTask<string?> GetSecureAsync(string key) => GetAsync(key);

    public ValueTask SetSecureAsync(string key, string? value) => SetAsync(key, value);
}
