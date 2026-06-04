using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches near real-time active fire detections from NASA FIRMS (US/Canada feed).
// Data comes from the VIIRS instrument on two satellites: Suomi NPP and NOAA-20.
// Each detection is a 375m satellite pixel where the sensor recorded significant heat.
// FIRMS updates roughly twice per day per satellite as they pass over Michigan.
// API docs: https://firms.modaps.eosdis.nasa.gov/api/area/
public class FirmsFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<FirmsFetcherFunction> logger)
{
    // Michigan bounding box (west, south, east, north) covering both peninsulas.
    private const string MichiganBbox = "-90.5,41.7,-82.1,48.3";

    // VIIRS provides 375m resolution hotspots. Fetching both satellites maximizes
    // coverage since their orbital passes over Michigan occur at different times.
    private static readonly string[] Sensors = ["VIIRS_SNPP_NRT", "VIIRS_NOAA20_NRT"];

    private const string FirmsBaseUrl = "https://firms.modaps.eosdis.nasa.gov/api/area/geojson";

    [Function("FirmsFetcher")]
    public async Task Run([TimerTrigger("0 0 */2 * * *")] TimerInfo timer)
    {
        logger.LogInformation("FIRMS fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("FIRMS fetcher is running behind schedule");

        var apiKey = config["FirmsApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("FirmsApiKey is not configured - skipping FIRMS fetch");
            return;
        }

        try
        {
            using var client = httpFactory.CreateClient();
            var upserted = 0;

            foreach (var sensor in Sensors)
            {
                // Request the last 1 day of detections. FIRMS caps the area endpoint
                // at 10 days maximum; 1 day keeps the result set small and fresh.
                // URL intentionally not logged to avoid exposing the API key.
                var url = $"{FirmsBaseUrl}/{apiKey}/{sensor}/{MichiganBbox}/1";
                logger.LogInformation("Fetching FIRMS data for sensor {Sensor}", sensor);
                var response = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);

                foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
                {
                    var props    = feature.GetProperty("properties");
                    var geometry = feature.GetProperty("geometry");
                    if (geometry.ValueKind == JsonValueKind.Null) continue;

                    // FIRMS GeoJSON uses standard [lng, lat] coordinate order.
                    var coords = geometry.GetProperty("coordinates");
                    var lng    = coords[0].GetDecimal();
                    var lat    = coords[1].GetDecimal();

                    // Skip low-confidence detections to reduce false positives from
                    // industrial heat sources, sun glint, and cloud-edge artifacts.
                    var confidence = props.TryGetProperty("confidence", out var c) ? c.GetString() : null;
                    if (string.Equals(confidence, "low", StringComparison.OrdinalIgnoreCase)) continue;

                    var acqDate  = props.TryGetProperty("acq_date", out var ad) ? ad.GetString() : null;
                    var acqTime  = props.TryGetProperty("acq_time", out var at) ? at.GetString() : null;
                    var frp      = props.TryGetProperty("frp",      out var f)  ? f.GetDouble()  : 0;
                    var satellite = props.TryGetProperty("satellite", out var s) ? s.GetString() : sensor;

                    // Stable key includes satellite + acquisition time + location so that
                    // two sensors detecting the same fire are stored as separate events,
                    // and re-fetching the same pass is idempotent.
                    var stableKey = $"firms|{satellite}|{acqDate}|{acqTime}|{lat:F4}|{lng:F4}";

                    var evt = new NaturalEvent
                    {
                        EventId     = EventNormalizer.StableGuid(stableKey),
                        EventType   = "WILDFIRE",
                        Title       = $"Active Fire Detection ({confidence ?? "nominal"} confidence)",
                        Description = frp > 0
                            ? $"Fire Radiative Power: {frp:N1} MW | Satellite: {satellite} | {acqDate} {FormatTime(acqTime)} UTC"
                            : $"Satellite: {satellite} | {acqDate} {FormatTime(acqTime)} UTC",
                        Severity    = MapFrpToSeverity(frp),
                        Lat         = lat,
                        Lng         = lng,
                        CountyFips  = null, // FIRMS gives lat/lng only - no county polygon lookup available
                        SourceUrl   = "https://firms.modaps.eosdis.nasa.gov/usfs/",
                        FetchedAt   = DateTime.UtcNow,
                        ExpiresAt   = DateTime.UtcNow.AddHours(48)
                    };

                    await repo.UpsertEventAsync(evt);
                    upserted++;
                }
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("FIRMS fetch complete - {Count} detection(s) upserted", upserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FIRMS fetcher failed");
        }
    }

    // FRP (Fire Radiative Power) in megawatts is the best available intensity signal.
    // Thresholds are based on FIRMS documentation and common field usage.
    private static string MapFrpToSeverity(double frp) => frp switch
    {
        >= 200 => "CRITICAL", // large, intense wildfire
        >= 50  => "HIGH",     // significant active fire
        >= 10  => "MODERATE", // active burn, moderate intensity
        _      => "LOW"       // low-intensity detection or smoldering
    };

    // FIRMS encodes time as a 4-digit string like "0142" meaning 01:42 UTC.
    private static string FormatTime(string? hhmm)
    {
        if (hhmm is null || hhmm.Length < 4) return "";
        return $"{hhmm[..2]}:{hhmm[2..]}";
    }
}
