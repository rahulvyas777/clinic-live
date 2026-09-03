using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// MAUI's answer: ask for the camera, push a native full-screen scanner page over the
/// Blazor UI, come back with the text. Blazor Hybrid's party trick — the WebView is
/// just one page in a native app, and native pages can be pushed on top of it.
/// </summary>
public sealed class CodeScanner : ICodeScanner
{
    // Phones and tablets have a camera you'd hold up to a ticket; a laptop webcam
    // pointing at your face does not count.
    public bool IsSupported =>
        DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS;

    public async Task<string?> ScanAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }
        if (status != PermissionStatus.Granted)
        {
            return null;
        }

        var host = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (host is null)
        {
            return null;
        }

        var page = new ScanPage();
        await host.Navigation.PushModalAsync(page);
        try
        {
            return await page.Result;
        }
        finally
        {
            if (host.Navigation.ModalStack.Contains(page))
            {
                await host.Navigation.PopModalAsync();
            }
        }
    }
}
