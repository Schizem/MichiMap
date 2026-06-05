using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches current air quality observations for Michigan from EPA AirNow every hour.
// All AQI readings are stored so the map shows the full state-wide AQ picture.
// Free API key registration: https://www.airnowapi.org/account/request/
public class EpaAirNowFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<EpaAirNowFetcherFunction> logger)
{
    // Two center points cover all of Michigan including the western Upper Peninsula.
    // The LP center (44.0, -84.5) and UP center (46.5, -87.5) each use a 200-mile radius.
    private static readonly (double Lat, double Lng, int Distance)[] Centers =
    [
        (44.0, -84.5, 200), // Lower Peninsula
        (46.5, -87.5, 200)  // Upper Peninsula
    ];

    private const string AirNowBaseUrl =
        "https://www.airnowapi.org/aq/observation/latLong/current/" +
        "?format=application/json&latitude={0}&longitude={1}&distance={2}&API_KEY={3}";

    [Function("EpaAirNowFetcher")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("EPA AirNow fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("EPA AirNow fetcher is running behind schedule");

        var apiKey = config["EpaAirNowApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("EpaAirNowApiKey not configured - skipping fetch");
            return;
        }

        try
        {
            using var client = httpFactory.CreateClient();
            var upserted = 0;

            // Track seen stations to avoid duplicate upserts when LP and UP radii overlap.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (lat, lng, dist) in Centers)
            {
                var url = string.Format(AirNowBaseUrl, lat, lng, dist, apiKey);
                var response = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);

                foreach (var obs in doc.RootElement.EnumerateArray())
                {
                    if (obs.GetProperty("StateCode").GetString() != "MI")
                        continue;

                    var reportingArea = obs.GetProperty("ReportingArea").GetString() ?? "Unknown";
                    var parameter     = obs.GetProperty("ParameterName").GetString() ?? "AQI";

                    // Stable key by area + parameter only (no date/hour) so each run
                    // upserts the same record rather than creating a new one per hour.
                    var stableKey = $"airnow|{reportingArea}|{parameter}";
                    if (!seen.Add(stableKey)) continue;

                    var aqi      = obs.GetProperty("AQI").GetInt32();
                    if (aqi <= 0) continue; // No valid reading

                    var severity = EventNormalizer.MapAqiSeverity(aqi);
                    var category = obs.TryGetProperty("Category", out var cat)
                        ? cat.GetProperty("Name").GetString() ?? "Unknown"
                        : "Unknown";

                    var obsLat = obs.GetProperty("Latitude").GetDecimal();
                    var obsLng = obs.GetProperty("Longitude").GetDecimal();

                    var evt = new NaturalEvent
                    {
                        EventId     = EventNormalizer.StableGuid(stableKey),
                        EventType   = "AIR_QUALITY",
                        Title       = $"Air Quality - {reportingArea}",
                        Description = $"{parameter}: AQI {aqi} ({category})",
                        Severity    = severity,
                        Lat         = obsLat,
                        Lng         = obsLng,
                        SourceUrl   = "https://www.airnow.gov/state/?name=michigan",
                        FetchedAt   = DateTime.UtcNow,
                        ExpiresAt   = DateTime.UtcNow.AddHours(3)
                    };

                    await repo.UpsertEventAsync(evt);
                    upserted++;
                }
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("EPA AirNow fetch complete - {Count} station(s) upserted", upserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "EPA AirNow fetcher failed");
        }
    }
}
