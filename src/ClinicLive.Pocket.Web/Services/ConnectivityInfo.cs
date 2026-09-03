using ClinicLive.Pocket.Shared.Services.Device;
using Microsoft.JSInterop;

namespace ClinicLive.Pocket.Web.Services;

/// <summary>
/// The browser's answer: navigator.onLine plus the online/offline events, delivered
/// back into .NET through a DotNetObjectReference. Per circuit (scoped) — it's one
/// visitor's browser. Can't start until the first render: prerendering has no JS.
/// </summary>
public sealed class ConnectivityInfo(IJSRuntime js) : IConnectivityInfo, IDisposable
{
    private DotNetObjectReference<ConnectivityInfo>? _self;
    private bool _online = true;

    public bool IsOnline => _online;

    public event Action<bool>? Changed;

    public async ValueTask EnsureStartedAsync()
    {
        if (_self is not null)
        {
            return;
        }
        _self = DotNetObjectReference.Create(this);
        _online = await js.InvokeAsync<bool>("pocketDevice.isOnline");
        await js.InvokeVoidAsync("pocketDevice.watchOnline", _self);
    }

    [JSInvokable]
    public void OnOnlineChanged(bool online)
    {
        _online = online;
        Changed?.Invoke(online);
    }

    public void Dispose() => _self?.Dispose();
}
