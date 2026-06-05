using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches current air quality observations for Michigan from EPA AirNow every hour.
// Queries each major Michigan metro area individually since the AirNow lat/lon
// radius endpoint returns only the nearest reporting area per query, not all
// areas within the radius.
public class EpaAirNowFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<EpaAirNowFetcherFunction> logger)
{
    // Major Michigan metros covering LP and UP — each with a 50-mile radius
    // to resolve the local AirNow reporting area for that region.
    private static readonly (double Lat, double Lng, string Label)[] MichiganLocations =
    [
        (42.33, -83.05, "Detroit"),
        (42.96, -85.67, "Grand Rapids"),
        (42.73, -84.55, "Lansing"),
        (43.01, -83.69, "Flint"),
        (43.42, -83.95, "Saginaw"),
        (42.28, -83.74, "Ann Arbor"),
        (42.29, -85.59, "Kalamazoo"),
        (43.23, -86.25, "Muskegon"),
        (44.76, -85.62, "Traverse City"),
        (43.59, -83.89, "Bay City"),
        (45.06, -83.44, "Alpena"),
        (46.54, -87.40, "Marquette"),
        (47.12, -88.57, "Houghton"),
        (46.46, -90.17, "Ironwood"),
        (46.49, -84.35, "Sault Ste. Marie")
    ];

    private const string AirNowBaseUrl =
        "https://www.airnowapi.org/aq/observation/latLong/current/" +
        "?format=application/json&latitude={0}&longitude={1}&distance=50&API_KEY={2}";

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
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (lat, lng, label) in MichiganLocations)
            {
                var url      = string.Format(AirNowBaseUrl, lat, lng, apiKey);
                var response = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);

                var observations = doc.RootElement.EnumerateArray()
                    .Where(o => !o.TryGetProperty("StateCode", out var sc) || sc.GetString() == "MI")
                    .Where(o => o.GetProperty("AQI").GetInt32() >= 0)
                    .ToList();

                logger.LogInformation("{Label}: {Count} observation(s) returned", label, observations.Count);

                // Group by reporting area and keep only the highest AQI reading.
                // This produces one marker per metro area regardless of how many
                // pollutants (PM2.5, O3, etc.) are being monitored.
                var byArea = observations
                    .GroupBy(o => o.GetProperty("ReportingArea").GetString() ?? label)
                    .Select(g => g.MaxBy(o => o.GetProperty("AQI").GetInt32())!);

                foreach (var obs in byArea)
                {
                    var reportingArea = obs.GetProperty("ReportingArea").GetString() ?? label;
                    var stableKey     = $"airnow|{reportingArea}";
                    if (!seen.Add(stableKey)) continue;

                    var aqi      = obs.GetProperty("AQI").GetInt32();
                    var parameter = obs.GetProperty("ParameterName").GetString() ?? "AQI";
                    var category  = obs.TryGetProperty("Category", out var cat)
                        ? cat.GetProperty("Name").GetString() ?? "Unknown"
                        : "Unknown";

                    var evt = new NaturalEvent
                    {
                        EventId     = EventNormalizer.StableGuid(stableKey),
                        EventType   = "AIR_QUALITY",
                        Title       = $"Air Quality - {reportingArea}",
                        Description = $"{parameter}: AQI {aqi} ({category})",
                        Severity    = EventNormalizer.MapAqiSeverity(aqi),
                        Lat         = obs.GetProperty("Latitude").GetDecimal(),
                        Lng         = obs.GetProperty("Longitude").GetDecimal(),
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
