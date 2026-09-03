using ClinicLive.Pocket.Shared.Services;
using ClinicLive.Pocket.Shared.Services.Device;

namespace ClinicLive.Tests;

public class GeoMathTests
{
    [Fact]
    public void Haversine_matches_a_known_distance()
    {
        // Greenwich Observatory to the Eiffel Tower: 333.8 km on a great circle.
        // (The first version of this test said "about 341" — the AI remembered
        // London–Paris city-centre distance, and Greenwich sits east of London. The
        // test failed, the formula was right, the expectation was wrong. Worked by
        // hand before changing the number.)
        var greenwich = new GeoPoint(51.4769, 0.0005);
        var eiffel = new GeoPoint(48.8584, 2.2945);

        var km = GeoMath.DistanceKm(greenwich, eiffel);

        Assert.InRange(km, 333, 335);
    }

    [Fact]
    public void Zero_distance_is_zero()
    {
        var p = new GeoPoint(37.43, -122.073);
        Assert.Equal(0, GeoMath.DistanceKm(p, p), precision: 9);
    }

    [Theory]
    [InlineData(0.35, "350 m")]
    [InlineData(1.34, "1.3 km")]
    [InlineData(12.6, "13 km")]
    public void Describe_uses_the_precision_people_use(double km, string expected)
    {
        Assert.Equal(expected, GeoMath.Describe(km));
    }
}
