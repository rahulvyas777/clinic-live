using ClinicLive.Pocket.Shared.Services.Device;
using Microsoft.JSInterop;

namespace ClinicLive.Pocket.Web.Services;

/// <summary>The browser's answer: the Geolocation API (secure context + user gesture), and Google Maps in a new tab.</summary>
public sealed class Locator(IJSRuntime js) : ILocator
{
    private sealed record JsPoint(double Latitude, double Longitude);

    public async Task<GeoPoint?> GetCurrentAsync()
    {
        var p = await js.InvokeAsync<JsPoint?>("pocketDevice.locate");
        return p is null ? null : new GeoPoint(p.Latitude, p.Longitude);
    }

    public Task<bool> OpenDirectionsAsync(GeoPoint destination, string label) =>
        js.InvokeAsync<bool>("pocketDevice.openDirections", destination.Latitude, destination.Longitude).AsTask();
}
