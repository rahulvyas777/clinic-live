namespace ClinicLive.Pocket.Shared.Services.Device;

/// <summary>
/// Capability #7: point the camera at a ticket. Supported means "this device has a
/// camera and a screen you'd hold up to a piece of paper" — phones and tablets.
/// The result is the RAW text of whatever was scanned; TicketCode decides if it's ours.
/// </summary>
public interface ICodeScanner
{
    bool IsSupported { get; }

    /// <summary>Opens the scanner; resolves with the decoded text, or null if the user cancelled or refused the camera.</summary>
    Task<string?> ScanAsync();
}
