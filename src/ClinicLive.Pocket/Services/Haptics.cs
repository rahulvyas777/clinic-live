using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// MAUI's answer, and for once one file covers every platform: HapticFeedback and
/// Vibration are MAUI Essentials, abstracted below us. (Android needs the VIBRATE
/// permission in the manifest for Vibration; HapticFeedback doesn't.)
/// </summary>
public sealed class Haptics : IHaptics
{
    public bool IsSupported => HapticFeedback.Default.IsSupported || Vibration.Default.IsSupported;

    public void Tap()
    {
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch (FeatureNotSupportedException)
        {
            // desktops and some tablets: silently nothing
        }
    }

    public void Buzz()
    {
        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(400));
        }
        catch (FeatureNotSupportedException)
        {
        }
    }
}
