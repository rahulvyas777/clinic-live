namespace ClinicLive.Pocket.Shared.Services.Device;

/// <summary>
/// Capability #3: physical feedback. Two verbs only — a phone app that vibrates for
/// everything is a phone app people mute.
/// </summary>
public interface IHaptics
{
    bool IsSupported { get; }

    /// <summary>A light click: "that tap registered".</summary>
    void Tap();

    /// <summary>An attention buzz: "look at me now" — it's your turn.</summary>
    void Buzz();
}
