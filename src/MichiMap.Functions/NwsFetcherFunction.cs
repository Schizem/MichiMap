using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches active flood warnings for Michigan from NOAA/NWS api.weather.gov every 15 minutes.
// AWS analogue: EventBridge Scheduler + Lambda.
// NWS User-Agent policy: https://www.weather.gov/documentation/services-web-api
public class NwsFetcherFunction(IEventRepository repo, IHttpClientFactory httpFactory, ILogger<NwsFetcherFunction> logger)
{
    private const string NwsAlertsUrl =
        "https://api.weather.gov/alerts/active?area=MI&event=" +
        "Flood%20Warning," +
        "Flood%20Advisory," +
        "Flash%20Flood%20Warning," +
        "Flash%20Flood%20Watch," +
        "Beach%20Hazards%20Statement," +
        "High%20Surf%20Advisory," +
        "Lakeshore%20Flood%20Advisory," +
        "Lakeshore%20Flood%20Warning," +
        "Lakeshore%20Flood%20Watch," +
        "Rip%20Current%20Statement";

    [Function("NwsFetcher")]
    public async Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("NWS fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("NWS fetcher is running behind schedule");

        try
        {
            using var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MichiMap/1.0 (contact@call-me-ty.com)");

            var response = await client.GetStringAsync(NwsAlertsUrl);
            using var doc = JsonDocument.Parse(response);

            var upserted = 0;
            foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var props = feature.GetProperty("properties");
                var geometry = feature.GetProperty("geometry");

                // NWS area-wide alerts have null geometry — skip; they lack a mappable location
                if (geometry.ValueKind == JsonValueKind.Null)
                    continue;

                var (lat, lng) = ParseGeometry(geometry);

                // NWS alert IDs are URNs like "urn:oid:2.49.0.1.840.0.ABC…" — not GUIDs.
                // Derive a stable GUID so repeated fetches upsert rather than insert duplicates.
                var nwsId = props.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                var eventId = EventNormalizer.StableGuid(nwsId);

                var evt = new NaturalEvent
                {
                    EventId     = eventId,
                    EventType   = "FLOOD",
                    Title       = props.GetProperty("headline").GetString() ?? "Flood Warning",
                    Description = props.GetProperty("description").GetString(),
                    Severity    = EventNormalizer.MapNwsSeverity(props.GetProperty("severity").GetString()),
                    Lat         = lat,
                    Lng         = lng,
                    SourceUrl   = props.GetProperty("@id").GetString(),
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

    private static (decimal lat, decimal lng) ParseGeometry(JsonElement geometry)
    {
        var type   = geometry.GetProperty("type").GetString();
        var coords = geometry.GetProperty("coordinates");

        return type switch
        {
            // Polygon: coordinates = [ [ [lng,lat], ... ] ]
            "Polygon" => EventNormalizer.PolygonCentroid(coords[0]),
            // MultiPolygon: coordinates = [ [ [ [lng,lat], ... ] ], ... ] — use first polygon's outer ring
            "MultiPolygon" => EventNormalizer.PolygonCentroid(coords[0][0]),
            // Point: coordinates = [lng, lat]
            _ => (coords[1].GetDecimal(), coords[0].GetDecimal())
        };
    }
}
