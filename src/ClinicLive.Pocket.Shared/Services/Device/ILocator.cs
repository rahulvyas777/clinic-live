namespace ClinicLive.Pocket.Shared.Services.Device;

public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// Capability #6: "where is this device, and can you take them somewhere?"
/// Both answers may legitimately be "no": permission refused, no fix indoors,
/// a desktop with no GPS, a browser without a secure context. The page copes.
/// </summary>
public interface ILocator
{
    /// <summary>The device's current position, or null if the user declined or nothing could be found in time.</summary>
    Task<GeoPoint?> GetCurrentAsync();

    /// <summary>Hand off to the platform's maps app with the destination set. False if nothing could open.</summary>
    Task<bool> OpenDirectionsAsync(GeoPoint destination, string label);
}
