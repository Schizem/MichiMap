using MichiMap.Functions.Normalizers;
using System.Text.Json;
using Xunit;

namespace MichiMap.Tests;

public class FetcherNormalizerTests
{
    // --- StableGuid ---

    [Fact]
    public void StableGuid_SameInput_ReturnsSameGuid()
    {
        var a = EventNormalizer.StableGuid("urn:oid:2.49.0.1.840.0.ABC123");
        var b = EventNormalizer.StableGuid("urn:oid:2.49.0.1.840.0.ABC123");
        Assert.Equal(a, b);
    }

    [Fact]
    public void StableGuid_DifferentInputs_ReturnDifferentGuids()
    {
        var a = EventNormalizer.StableGuid("alert-1");
        var b = EventNormalizer.StableGuid("alert-2");
        Assert.NotEqual(a, b);
    }

    // --- MapNwsSeverity ---

    [Theory]
    [InlineData("Extreme",  "CRITICAL")]
    [InlineData("Severe",   "HIGH")]
    [InlineData("Moderate", "MODERATE")]
    [InlineData("Minor",    "LOW")]
    public void MapNwsSeverity_KnownValues_MapsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, EventNormalizer.MapNwsSeverity(input));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void MapNwsSeverity_UnknownValues_ReturnsNull(string? input)
    {
        Assert.Null(EventNormalizer.MapNwsSeverity(input));
    }

    // --- MapAqiSeverity ---

    [Theory]
    [InlineData(0,   null)]       // Good
    [InlineData(50,  null)]       // Good
    [InlineData(100, null)]       // Moderate boundary — still not a map marker
    [InlineData(101, "MODERATE")] // USG threshold
    [InlineData(150, "MODERATE")] // USG boundary
    [InlineData(151, "HIGH")]     // Unhealthy
    [InlineData(200, "HIGH")]     // Unhealthy boundary
    [InlineData(201, "CRITICAL")] // Very Unhealthy
    [InlineData(500, "CRITICAL")] // Hazardous
    public void MapAqiSeverity_CorrectBuckets(int aqi, string? expected)
    {
        Assert.Equal(expected, EventNormalizer.MapAqiSeverity(aqi));
    }

    // --- PolygonCentroid ---

    [Fact]
    public void PolygonCentroid_AxisAlignedSquare_ReturnsCenterPoint()
    {
        // Square: (0,0) (2,0) (2,2) (0,2) (0,0) — GeoJSON uses [lng, lat]
        var json = """[[0,0],[2,0],[2,2],[0,2],[0,0]]""";
        var ring = JsonDocument.Parse(json).RootElement;

        var (lat, lng) = EventNormalizer.PolygonCentroid(ring);

        Assert.Equal(1m, lat);
        Assert.Equal(1m, lng);
    }

    [Fact]
    public void PolygonCentroid_RealMichiganBounds_WithinStateLatLng()
    {
        // Rough bounding box for Michigan Lower Peninsula
        var json = """[[-87,41.7],[-82,41.7],[-82,45.9],[-87,45.9],[-87,41.7]]""";
        var ring = JsonDocument.Parse(json).RootElement;

        var (lat, lng) = EventNormalizer.PolygonCentroid(ring);

        Assert.InRange(lat, 41m, 48m);
        Assert.InRange(lng, -91m, -82m);
    }
}
