using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Pocket.Services;

/// <summary>
/// MAUI's answer: Geolocation and Map from MAUI Essentials — one file, every platform.
/// The permission is requested explicitly here so the moment is ours to choose (the
/// user just tapped "Use my location"), not the framework's.
/// </summary>
public sealed class Locator : ILocator
{
    public async Task<GeoPoint?> GetCurrentAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }
            if (status != PermissionStatus.Granted)
            {
                return null;
            }

            // Medium accuracy is plenty for "how far is the clinic" and kinder to the
            // battery; ten seconds is the most a person will wait staring at "Finding you…".
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request)
                           ?? await Geolocation.Default.GetLastKnownLocationAsync();

            return location is null ? null : new GeoPoint(location.Latitude, location.Longitude);
        }
        catch (FeatureNotSupportedException)
        {
            return null;   // no GPS hardware (most desktops)
        }
        catch (FeatureNotEnabledException)
        {
            return null;   // location services switched off on the device
        }
        catch (PermissionException)
        {
            return null;
        }
    }

    public async Task<bool> OpenDirectionsAsync(GeoPoint destination, string label)
    {
        try
        {
            // Google Maps on Android, Apple Maps on iOS, the Maps app on Windows —
            // whichever the platform considers "the maps app".
            return await Map.Default.TryOpenAsync(destination.Latitude, destination.Longitude,
                new MapLaunchOptions { Name = label, NavigationMode = NavigationMode.Driving });
        }
        catch (Exception)
        {
            return false;
        }
    }
}
