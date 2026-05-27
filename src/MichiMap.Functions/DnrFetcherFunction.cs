using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches Michigan DNR prescribed burn records from the Michigan Open Data fire history service.
// Data source: Michigan DNR via ArcGIS Hub: https://gis-michigan.opendata.arcgis.com/
// TODO: verify the layer index and field names against the live service before deploying
public class DnrFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    MichiganCountyService counties,
    ILogger<DnrFetcherFunction> logger)
{
    private const string BurnsUrl =
        "https://services3.arcgis.com/Jdnp1TjADvSDxMAX/arcgis/rest/services/pub_MiMorelsApp/FeatureServer/0/query" +
        "?where=Fire_Type%3D'Prescribed+Burn'&outFields=*&f=geojson";

    [Function("DnrBurnsFetcher")]
    public async Task Run([TimerTrigger("0 0 6 * * *")] TimerInfo timer)
    {
        logger.LogInformation("DNR burns fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("DNR burns fetcher is running behind schedule");

        try
        {
            using var client = httpFactory.CreateClient();
            var response = await client.GetStringAsync(BurnsUrl);
            using var doc = JsonDocument.Parse(response);

            var upserted = 0;
            foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var props    = feature.GetProperty("properties");
                var geometry = feature.GetProperty("geometry");
                if (geometry.ValueKind == JsonValueKind.Null) continue;

                var (lat, lng) = ParseGeometry(geometry);
                if (lat == 0m && lng == 0m) continue;

                var countyName = props.TryGetProperty("County_Name", out var cn) ? cn.GetString() : null;
                var acres      = props.TryGetProperty("AcresBurned", out var ab) ? ab.GetDouble() : 0;
                var year       = props.TryGetProperty("YearOccurred", out var yo) ? yo.GetInt32() : 0;
                var countyInfo = countyName is not null ? counties.Lookup(countyName) : null;

                var stableKey = $"dnrburn|{countyName}|{year}|{lat:F4}|{lng:F4}";

                var evt = new NaturalEvent
                {
                    EventId     = EventNormalizer.StableGuid(stableKey),
                    EventType   = "BURN",
                    Title       = $"Prescribed Burn — {countyName ?? "Michigan"} County{(year > 0 ? $" ({year})" : "")}",
                    Description = acres > 0 ? $"Area burned: {acres:N0} acres" : null,
                    Severity    = "LOW",
                    Lat         = lat,
                    Lng         = lng,
                    CountyFips  = countyInfo?.Fips,
                    SourceUrl   = "https://gis-michigan.opendata.arcgis.com/",
                    FetchedAt   = DateTime.UtcNow,
                    ExpiresAt   = year > 0 ? new DateTime(year + 1, 7, 1, 0, 0, 0, DateTimeKind.Utc) : null
                };

                await repo.UpsertEventAsync(evt);
                upserted++;
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("DNR burns fetch complete — {Count} burn(s) upserted", upserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DNR burns fetcher failed");
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
}
