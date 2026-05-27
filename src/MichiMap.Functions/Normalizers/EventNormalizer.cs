using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MichiMap.Functions.Normalizers;

public static class EventNormalizer
{
    // Derives a deterministic GUID from an arbitrary string ID (a NWS URN or ArcGIS GlobalID).
    // Guarantees the same input always produces the same GUID for idempotent upserts.
    public static Guid StableGuid(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }

    // Computes the arithmetic centroid of a GeoJSON polygon ring.
    // ring: a JsonElement that is an array of [lng, lat] coordinate pairs.
    public static (decimal lat, decimal lng) PolygonCentroid(JsonElement ring)
    {
        var points = ring.EnumerateArray().ToList();
        // GeoJSON polygon rings close by repeating the first vertex
        if (points.Count > 1)
            points = points.Take(points.Count - 1).ToList();
        if (points.Count == 0) return (0m, 0m);

        var avgLat = points.Select(p => p[1].GetDecimal()).Average();
        var avgLng = points.Select(p => p[0].GetDecimal()).Average();
        return (avgLat, avgLng);
    }

    // NWS severity string -> MichiMap severity.
    public static string? MapNwsSeverity(string? nws) => nws switch
    {
        "Extreme"  => "CRITICAL",
        "Severe"   => "HIGH",
        "Moderate" => "MODERATE",
        "Minor"    => "LOW",
        _          => null
    };

    // EPA AQI value -> MichiMap severity.
    // Only returns non-null for AQI > 100 (Unhealthy for Sensitive Groups or worse).
    public static string? MapAqiSeverity(int aqi) => aqi switch
    {
        <= 100 => null,      // Good / Moderate
        <= 150 => "MODERATE", // Unhealthy for Sensitive Groups
        <= 200 => "HIGH",    // Unhealthy
        _      => "CRITICAL" // Very Unhealthy / Hazardous
    };

    // Parses a nullable ISO-8601 string, returning null if missing or empty.
    public static DateTime? ParseNullableDate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : DateTime.Parse(value).ToUniversalTime();
}
