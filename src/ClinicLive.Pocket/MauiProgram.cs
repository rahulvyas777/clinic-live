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
        builder.Services.AddSingleton<IAppLifecycle>(AppLifecycle.Instance);
        builder.Services.AddSingleton<IHaptics, Haptics>();
        builder.Services.AddSingleton<INotifier, Notifier>();

        // The clinic's API and live queue — aimed at wherever "the server" is from
        // this device (see ApiEndpoint).
        builder.Services.AddSingleton(new ClinicEndpoint(ApiEndpoint.Base));
        builder.Services.AddSingleton(new HttpClient { BaseAddress = ApiEndpoint.Base, Timeout = TimeSpan.FromSeconds(10) });
        builder.Services.AddSingleton<PocketApi>();
        builder.Services.AddSingleton<QueueLive>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
