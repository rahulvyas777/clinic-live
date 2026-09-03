using ClinicLive.Pocket.Shared.Services.Device;
using Microsoft.JSInterop;

namespace ClinicLive.Pocket.Web.Services;

/// <summary>
/// The browser's answer: navigator.vibrate, which exists on Android Chrome and
/// nowhere else that matters. Fire-and-forget — nobody waits for a buzz.
/// </summary>
public sealed class Haptics(IJSRuntime js) : IHaptics
{
    // We can't know from the server whether the visitor's browser has a motor.
    public bool IsSupported => true;

    public void Tap() => _ = js.InvokeAsync<bool>("pocketDevice.vibrate", 30).AsTask();

    public void Buzz() => _ = js.InvokeAsync<bool>("pocketDevice.vibrate", new[] { 200, 100, 200 }).AsTask();
}
