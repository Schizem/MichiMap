using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches current air quality observations for Michigan from EPA AirNow every hour.
// Free API key registration: https://www.airnowapi.org/account/request/
// Only creates map markers for AQI > 100 (Unhealthy for Sensitive Groups or worse).
public class EpaAirNowFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<EpaAirNowFetcherFunction> logger)
{
    // Center of Michigan Lower Peninsula — 300-mile radius covers the entire state
    private const string AirNowUrl =
        "https://www.airnowapi.org/aq/observation/latLong/current/" +
        "?format=application/json&latitude=44.0&longitude=-84.5&distance=300&API_KEY={0}";

    [Function("EpaAirNowFetcher")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("EPA AirNow fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("EPA AirNow fetcher is running behind schedule");

        var apiKey = config["EpaAirNowApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("EpaAirNowApiKey not configured — skipping fetch");
            return;
        }

        try
        {
            using var client = httpFactory.CreateClient();
            var url = string.Format(AirNowUrl, apiKey);
            var response = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            var upserted = 0;
            foreach (var obs in doc.RootElement.EnumerateArray())
            {
                // Filter to Michigan only (the 300-mile radius includes parts of neighboring states)
                if (obs.GetProperty("StateCode").GetString() != "MI")
                    continue;

                var aqi = obs.GetProperty("AQI").GetInt32();
                var severity = EventNormalizer.MapAqiSeverity(aqi);

                // Skip Good/Moderate readings — not worth a map marker
                if (severity is null)
                    continue;

                var reportingArea = obs.GetProperty("ReportingArea").GetString() ?? "Unknown";
                var parameter     = obs.GetProperty("ParameterName").GetString() ?? "AQI";
                var lat           = obs.GetProperty("Latitude").GetDecimal();
                var lng           = obs.GetProperty("Longitude").GetDecimal();
                var dateObs       = obs.GetProperty("DateObserved").GetString()?.Trim() ?? "";
                var hourObs       = obs.GetProperty("HourObserved").GetInt32();

                // Stable ID: reporting area + date + hour + parameter (handles re-runs in the same hour)
                var stableKey = $"airnow|{reportingArea}|{dateObs}|{hourObs}|{parameter}";

                var evt = new NaturalEvent
                {
                    EventId     = EventNormalizer.StableGuid(stableKey),
                    EventType   = "AIR_QUALITY",
                    Title       = $"Air Quality Alert — {reportingArea}",
                    Description = $"{parameter}: AQI {aqi} ({obs.GetProperty("Category").GetProperty("Name").GetString()})",
                    Severity    = severity,
                    Lat         = lat,
                    Lng         = lng,
                    SourceUrl   = "https://www.airnow.gov/",
                    FetchedAt   = DateTime.UtcNow,
                    ExpiresAt   = DateTime.UtcNow.AddHours(2)
                };

                await repo.UpsertEventAsync(evt);
                upserted++;
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("EPA AirNow fetch complete — {Count} alert(s) upserted", upserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "EPA AirNow fetcher failed");
        }
    }
}
