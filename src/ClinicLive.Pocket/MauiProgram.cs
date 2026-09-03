using Microsoft.Extensions.Logging;
using ClinicLive.Pocket.Services;
using ClinicLive.Pocket.Shared.Services;
using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Device capabilities the shared UI asks for. Each interface lives in the
        // shared project; each implementation lives here, next to the platform it
        // actually talks to. The web host registers its own answers to the same
        // questions.
        builder.Services.AddSingleton<IPlatformInfo, PlatformInfo>();

        // The clinic's API — one HttpClient for the app's lifetime, aimed at wherever
        // "the server" is from this device (see ApiEndpoint).
        builder.Services.AddSingleton(new HttpClient { BaseAddress = ApiEndpoint.Base, Timeout = TimeSpan.FromSeconds(10) });
        builder.Services.AddSingleton<PocketApi>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
