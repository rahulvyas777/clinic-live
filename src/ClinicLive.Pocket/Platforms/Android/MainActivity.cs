using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace ClinicLive.Pocket;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // The first screenshot: our header sat BEHIND the status bar and the tab labels
        // BEHIND the gesture bar. Android hands the WebView the whole screen, and CSS's
        // env(safe-area-inset-*) reads 0 inside a WebView — so the insets have to be
        // applied here, natively, as padding on the content view.
        var content = FindViewById<ViewGroup>(Android.Resource.Id.Content);
        if (content is not null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarsPadding());
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(21) && Window is not null)
        {
            Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#16696F"));
        }
    }

    private sealed class SystemBarsPadding : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        // Android.Views.View spelled out: inside a MAUI project, bare "View" is
        // Microsoft.Maui.Controls.View — the compiler's first complaint on this file.
        public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (v is null || insets is null)
            {
                return insets ?? WindowInsetsCompat.Consumed!;
            }

            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            return WindowInsetsCompat.Consumed!;
        }
    }
}
