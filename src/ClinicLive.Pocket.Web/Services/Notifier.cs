using ClinicLive.Pocket.Shared.Services.Device;
using Microsoft.JSInterop;

namespace ClinicLive.Pocket.Web.Services;

/// <summary>The browser's answer: the Notification API — which the browser only honours while the tab is open.</summary>
public sealed class Notifier(IJSRuntime js) : INotifier
{
    public async Task<bool> RequestPermissionAsync() =>
        await js.InvokeAsync<bool>("pocketDevice.requestNotifications");

    public async Task ShowAsync(string title, string body) =>
        await js.InvokeAsync<bool>("pocketDevice.notify", title, body);
}
