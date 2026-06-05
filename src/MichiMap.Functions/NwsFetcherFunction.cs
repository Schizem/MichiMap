using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches all active NWS weather alerts for Michigan every 15 minutes.
// Flood-specific events map to EventType FLOOD; everything else maps to WEATHER.
// Zone-based alerts (beach hazards, etc.) have null geometry — their location is
// resolved from the NWS SAME geocode (county FIPS) using MichiganCountyService.
public class NwsFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    MichiganCountyService counties,
    ILogger<NwsFetcherFunction> logger)
{
    private const string NwsUrl = "https://api.weather.gov/alerts/active/area/MI";

    // NWS event types treated as flood events; all others become WEATHER.
    private static readonly HashSet<string> FloodEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "Flood Warning", "Flood Advisory", "Flash Flood Warning", "Flash Flood Watch",
        "Flash Flood Statement", "Flood Statement", "Flood Watch",
        "Lakeshore Flood Advisory", "Lakeshore Flood Warning", "Lakeshore Flood Watch"
    };

    [Function("NwsFetcher")]
    public async Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("NWS fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("NWS fetcher is running behind schedule");

        try
        {
            using var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MichiMap/1.0 (haytyler7@gmail.com)");

            var response = await client.GetStringAsync(NwsUrl);
            using var doc = JsonDocument.Parse(response);

            var upserted = 0;
            foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var props    = feature.GetProperty("properties");
                var geometry = feature.GetProperty("geometry");

                decimal lat = 0, lng = 0;
                string? countyFips = null;

                if (geometry.ValueKind != JsonValueKind.Null)
                {
                    (lat, lng) = ParseGeometry(geometry);
                }
                else
                {
                    // Zone-based alerts have no geometry. Resolve from the NWS SAME geocode,
                    // which contains county FIPS codes prefixed with a leading zero (e.g. "026061").
                    var countyInfo = ResolveSameGeocode(props);
                    if (countyInfo is null) continue;
                    lat        = countyInfo.Lat;
                    lng        = countyInfo.Lng;
                    countyFips = countyInfo.Fips;
                }

                if (lat == 0m && lng == 0m) continue;

                var nwsId   = props.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var eventId = EventNormalizer.StableGuid(nwsId);

                var eventName = props.TryGetProperty("event", out var evEl) ? evEl.GetString() : null;
                var eventType = FloodEvents.Contains(eventName ?? "") ? "FLOOD" : "WEATHER";

                var evt = new NaturalEvent
                {
                    EventId     = eventId,
                    EventType   = eventType,
                    Title       = props.TryGetProperty("headline", out var hl) && hl.ValueKind == JsonValueKind.String
                                    ? hl.GetString() ?? eventName ?? "Weather Alert"
                                    : eventName ?? "Weather Alert",
                    Description = props.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String
                                    ? desc.GetString()
                                    : null,
                    Severity    = EventNormalizer.MapNwsSeverity(
                                    props.TryGetProperty("severity", out var sev) ? sev.GetString() : null),
                    Lat         = lat,
                    Lng         = lng,
                    CountyFips  = countyFips,
                    SourceUrl   = props.TryGetProperty("@id", out var alertUrl) && alertUrl.ValueKind == JsonValueKind.String
                                    ? alertUrl.GetString()
                                    : "https://api.weather.gov/alerts/active/area/MI",
                    FetchedAt   = DateTime.UtcNow,
                    ExpiresAt   = props.TryGetProperty("expires", out var exp) && exp.ValueKind != JsonValueKind.Null
                                    ? DateTime.Parse(exp.GetString()!).ToUniversalTime()
                                    : DateTime.UtcNow.AddHours(6)
                };

                await repo.UpsertEventAsync(evt);
                upserted++;
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("NWS fetch complete — {Count} alert(s) upserted", upserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NWS fetcher failed");
        }
    }

    // Extracts the first Michigan county centroid from the NWS SAME geocode array.
    // SAME codes are 6 digits with a leading 0: "026061" → FIPS "26061" (Houghton).
    private CountyInfo? ResolveSameGeocode(JsonElement props)
    {
        if (!props.TryGetProperty("geocode", out var geocode)) return null;
        if (!geocode.TryGetProperty("SAME", out var same))     return null;

        foreach (var code in same.EnumerateArray())
        {
            var raw = code.GetString();
            if (raw is null || raw.Length < 6) continue;

            // Strip the leading 0 to get a standard 5-digit FIPS.
            var fips = raw.TrimStart('0').PadLeft(5, '0');
            if (!fips.StartsWith("26")) continue; // Michigan only

            var info = counties.LookupByFips(fips);
            if (info is not null) return info;
        }

        return null;
    }

    private static (decimal lat, decimal lng) ParseGeometry(JsonElement geometry)
    {
        var type   = geometry.GetProperty("type").GetString();
        var coords = geometry.GetProperty("coordinates");

        return type switch
        {
            "Polygon"      => EventNormalizer.PolygonCentroid(coords[0]),
            "MultiPolygon" => EventNormalizer.PolygonCentroid(coords[0][0]),
            _              => (coords[1].GetDecimal(), coords[0].GetDecimal())
        };
    }
}
