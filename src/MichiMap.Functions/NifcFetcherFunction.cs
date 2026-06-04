using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches Michigan wildfire records from the Michigan DNR ArcGIS fire service.
// Endpoint verified 2026-06-04 against:
// https://services3.arcgis.com/Jdnp1TjADvSDxMAX/arcgis/rest/services/pub_MiMorelsApp/FeatureServer/0
public class NifcFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    MichiganCountyService counties,
    ILogger<NifcFetcherFunction> logger)
{
    // outSR=4326 forces the GeoJSON coordinates to be returned in lat/lng.
    // Without it, the service returns coordinates in Web Mercator (EPSG:3857).
    // YearOccurred>=2010 keeps the dataset to recent history; older records are
    // expired by the ExpiresAt = year+1 logic below anyway.
    private const string FireBaseUrl =
        "https://services3.arcgis.com/Jdnp1TjADvSDxMAX/arcgis/rest/services/pub_MiMorelsApp/FeatureServer/0/query" +
        "?where=Fire_Type%3D'Wildfire'%20AND%20YearOccurred%3E%3D2010&outFields=*&outSR=4326&f=geojson";

    private const int PageSize = 1000;

    [Function("DnrWildfireFetcher")]
    public async Task Run([TimerTrigger("0 0 6 * * *")] TimerInfo timer)
    {
        logger.LogInformation("DNR wildfire fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("DNR wildfire fetcher is running behind schedule");

        try
        {
            using var client = httpFactory.CreateClient();
            var offset = 0;
            var total  = 0;

            while (true)
            {
                var url      = $"{FireBaseUrl}&resultRecordCount={PageSize}&resultOffset={offset}";
                var response = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);

                var features = doc.RootElement.GetProperty("features").EnumerateArray().ToList();
                if (features.Count == 0) break;

                foreach (var feature in features)
                {
                    var props    = feature.GetProperty("properties");
                    var geometry = feature.GetProperty("geometry");
                    if (geometry.ValueKind == JsonValueKind.Null) continue;

                    var (lat, lng) = ParseGeometry(geometry);
                    if (lat == 0m && lng == 0m) continue;

                    // County_Name comes back as e.g. "Allegan County" - strip the suffix
                    // before passing to counties.Lookup(), which expects just "Allegan".
                    var rawCounty  = props.TryGetProperty("County_Name", out var cn) ? cn.GetString() : null;
                    var countyName = StripCountySuffix(rawCounty);
                    var acres      = props.TryGetProperty("AcresBurned",  out var ab) ? ab.GetDouble() : 0;
                    var year       = props.TryGetProperty("YearOccurred", out var yo) ? yo.GetInt32()  : 0;
                    var countyInfo = countyName is not null ? counties.Lookup(countyName) : null;

                    var stableKey = $"dnrfire|{countyName}|{year}|{lat:F4}|{lng:F4}";

                    var evt = new NaturalEvent
                    {
                        EventId     = EventNormalizer.StableGuid(stableKey),
                        EventType   = "WILDFIRE",
                        Title       = $"Wildfire - {countyName ?? "Michigan"} County{(year > 0 ? $" ({year})" : "")}",
                        Description = acres > 0 ? $"Area burned: {acres:N0} acres" : null,
                        Severity    = acres >= 1000 ? "CRITICAL" : acres >= 100 ? "HIGH" : "MODERATE",
                        Lat         = lat,
                        Lng         = lng,
                        CountyFips  = countyInfo?.Fips,
                        SourceUrl   = "https://gis-michigan.opendata.arcgis.com/",
                        FetchedAt   = DateTime.UtcNow,
                        ExpiresAt   = year > 0 ? new DateTime(year + 1, 7, 1, 0, 0, 0, DateTimeKind.Utc) : null,
                        EventYear   = year > 0 ? year : null
                    };

                    await repo.UpsertEventAsync(evt);
                    total++;
                }

                offset += PageSize;
                if (features.Count < PageSize) break;
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("DNR wildfire fetch complete - {Count} record(s) upserted", total);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DNR wildfire fetcher failed");
        }
    }

    private static (decimal lat, decimal lng) ParseGeometry(JsonElement geometry)
    {
        var geoType = geometry.GetProperty("type").GetString();
        var coords  = geometry.GetProperty("coordinates");
        return geoType switch
        {
            "Point"        => (coords[1].GetDecimal(), coords[0].GetDecimal()),
            "Polygon"      => EventNormalizer.PolygonCentroid(coords[0]),
            "MultiPolygon" => EventNormalizer.PolygonCentroid(coords[0][0]),
            _              => (0m, 0m)
        };
    }

    private static string? StripCountySuffix(string? name) =>
        name?.Replace(" County", "", StringComparison.OrdinalIgnoreCase).Trim();
}
