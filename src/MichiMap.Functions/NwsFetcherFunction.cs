using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches active flood warnings for Michigan from NOAA/NWS api.weather.gov every 15 minutes.
// AWS analogue: EventBridge Scheduler + Lambda.
public class NwsFetcherFunction(IEventRepository repo, IHttpClientFactory httpFactory, ILogger<NwsFetcherFunction> logger)
{
    private const string NwsAlertsUrl = "https://api.weather.gov/alerts/active?area=MI&event=Flood%20Warning";

    [Function("NwsFetcher")]
    public async Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("NWS fetcher triggered at {Time}", DateTime.UtcNow);

        using var client = httpFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "MichiMap/1.0 (contact@call-me-ty.com)");

        var response = await client.GetStringAsync(NwsAlertsUrl);
        using var doc = JsonDocument.Parse(response);

        foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            var props = feature.GetProperty("properties");
            var geometry = feature.GetProperty("geometry");

            // skip NWS alerts that may have null geometry
            if (geometry.ValueKind == JsonValueKind.Null)
                continue;

            var coords = geometry.GetProperty("coordinates");
            // Polygon centroid approximation
            var firstRing = coords[0];
            var lng = firstRing[0][0].GetDecimal();
            var lat = firstRing[0][1].GetDecimal();

            var evt = new NaturalEvent
            {
                EventId = Guid.Parse(props.GetProperty("id").GetString()!.Split('/').Last()),
                EventType = "FLOOD",
                Title = props.GetProperty("headline").GetString() ?? "Flood Warning",
                Description = props.GetProperty("description").GetString(),
                Severity = MapNwsSeverity(props.GetProperty("severity").GetString()),
                Lat = lat,
                Lng = lng,
                SourceUrl = props.GetProperty("@id").GetString(),
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = props.TryGetProperty("expires", out var exp)
                    ? DateTime.Parse(exp.GetString()!)
                    : DateTime.UtcNow.AddHours(6)
            };

            await repo.UpsertEventAsync(evt);
        }

        await repo.SoftDeleteExpiredAsync();
        logger.LogInformation("NWS fetch complete");
    }

    private static string? MapNwsSeverity(string? nws) => nws switch
    {
        "Extreme" => "CRITICAL",
        "Severe" => "HIGH",
        "Moderate" => "MODERATE",
        "Minor" => "LOW",
        _ => null
    };
}
