using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Shared.Services;

/// <summary>
/// Great-circle distance (haversine). MAUI has Location.CalculateDistance, but this
/// project is shared with a browser host that has no MAUI — and the formula is
/// fifteen lines that every developer should have typed once.
/// </summary>
public static class GeoMath
{
    private const double EarthRadiusKm = 6371.0088;

    public static double DistanceKm(GeoPoint a, GeoPoint b)
    {
        var dLat = ToRadians(b.Latitude - a.Latitude);
        var dLng = ToRadians(b.Longitude - a.Longitude);
        var lat1 = ToRadians(a.Latitude);
        var lat2 = ToRadians(b.Latitude);

        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(h));
    }

    /// <summary>"350 m", "1.3 km", "12 km" — the precision a person walking or driving actually uses.</summary>
    public static string Describe(double km) => km switch
    {
        < 0.95 => $"{Math.Round(km * 100) * 10:0} m",
        < 10 => $"{km:0.0} km",
        _ => $"{km:0} km",
    };

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
