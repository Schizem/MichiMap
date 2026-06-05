using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches all active NWS weather alerts for Michigan every 15 minutes.
// Zone-based alerts (beach hazards, etc.) expand to one map marker per affected
// Michigan county so each county shows its own pin rather than a shared point.
public class NwsFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    MichiganCountyService counties,
    ILogger<NwsFetcherFunction> logger)
{
    private const string NwsUrl = "https://api.weather.gov/alerts/active/area/MI";

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

                var nwsId     = props.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var eventName = props.TryGetProperty("event", out var evEl) ? evEl.GetString() : null;
                var eventType = FloodEvents.Contains(eventName ?? "") ? "FLOOD" : "WEATHER";
                var expires   = props.TryGetProperty("expires", out var exp) && exp.ValueKind != JsonValueKind.Null
                                    ? DateTime.Parse(exp.GetString()!).ToUniversalTime()
                                    : DateTime.UtcNow.AddHours(6);
                var sourceUrl = props.TryGetProperty("@id", out var alertUrl) && alertUrl.ValueKind == JsonValueKind.String
                                    ? alertUrl.GetString()
                                    : "https://api.weather.gov/alerts/active/area/MI";

                if (geometry.ValueKind != JsonValueKind.Null)
                {
                    // Point or polygon alert - single location from geometry.
                    var (lat, lng) = ParseGeometry(geometry);
                    if (lat == 0m && lng == 0m) continue;

                    await UpsertAlert(nwsId, props, eventName, eventType, lat, lng, null, expires, sourceUrl);
                    upserted++;
                }
                else
                {
                    // Zone-based alert (beach hazards, advisories, etc.) - expand to one
                    // marker per affected Michigan county so each county gets its own pin.
                    var miCounties = ResolveMichiganCounties(props);
                    if (miCounties.Count == 0) continue;

                    foreach (var county in miCounties)
                    {
                        // Include county FIPS in the stable key so each county is a distinct record.
                        await UpsertAlert($"{nwsId}|{county.Fips}", props, eventName, eventType,
                                          county.Lat, county.Lng, county.Fips, expires, sourceUrl);
                        upserted++;
                    }
                }
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("NWS fetch complete - {Count} alert marker(s) upserted", upserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NWS fetcher failed");
        }
    }

    private async Task UpsertAlert(
        string stableKey, JsonElement props, string? eventName, string eventType,
        decimal lat, decimal lng, string? countyFips, DateTime expires, string? sourceUrl)
    {
        var evt = new NaturalEvent
        {
            EventId     = EventNormalizer.StableGuid(stableKey),
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
            SourceUrl   = sourceUrl,
            FetchedAt   = DateTime.UtcNow,
            ExpiresAt   = expires
        };

        await repo.UpsertEventAsync(evt);
    }

    // Returns centroid info for every Michigan county referenced in the alert's SAME geocode.
    // SAME codes are 6 digits with a leading 0 (e.g. "026061" → FIPS "26061").
    private List<CountyInfo> ResolveMichiganCounties(JsonElement props)
    {
        var result = new List<CountyInfo>();
        if (!props.TryGetProperty("geocode", out var geocode)) return result;
        if (!geocode.TryGetProperty("SAME", out var same))     return result;

        foreach (var code in same.EnumerateArray())
        {
            var raw = code.GetString();
            if (raw is null || raw.Length < 6) continue;

            var fips = raw.TrimStart('0').PadLeft(5, '0');
            if (!fips.StartsWith("26")) continue; // Michigan FIPS prefix

            var info = counties.LookupByFips(fips);
            if (info is not null && result.All(r => r.Fips != info.Fips))
                result.Add(info);
        }

        return result;
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
