using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Functions.Normalizers;

namespace MichiMap.Functions;

// Fetches near real-time active fire detections from NASA FIRMS.
// Data comes from the VIIRS instrument on Suomi NPP and NOAA-20 satellites.
// Each detection is a 375m pixel where the sensor recorded significant heat.
// API docs: https://firms.modaps.eosdis.nasa.gov/api/area/
public class FirmsFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<FirmsFetcherFunction> logger)
{
    // Michigan bounding box: west, south, east, north — covers both peninsulas.
    private const string MichiganBbox = "-90.5,41.7,-82.1,48.3";

    // FIRMS CSV endpoint — GeoJSON is not supported for the area API.
    private const string FirmsBaseUrl = "https://firms.modaps.eosdis.nasa.gov/api/area/csv";

    private static readonly string[] Sensors = ["VIIRS_SNPP_NRT", "VIIRS_NOAA20_NRT"];

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
                // URL intentionally not logged to avoid exposing the API key.
                // 7-day window: gives meaningful coverage since Michigan rarely has daily detections.
                var url = $"{FirmsBaseUrl}/{apiKey}/{sensor}/{MichiganBbox}/7";
                logger.LogInformation("Fetching FIRMS CSV for sensor {Sensor}", sensor);

                var csv = await client.GetStringAsync(url);
                var rows = ParseCsv(csv);
                logger.LogInformation("Parsed {Count} rows from {Sensor}", rows.Count, sensor);

                foreach (var row in rows)
                {
                    if (!row.TryGetValue("latitude",  out var latStr)  || !decimal.TryParse(latStr,  out var lat)) continue;
                    if (!row.TryGetValue("longitude", out var lngStr)  || !decimal.TryParse(lngStr,  out var lng)) continue;

                    row.TryGetValue("confidence", out var confidence);
                    if (string.Equals(confidence, "low", StringComparison.OrdinalIgnoreCase)) continue;

                    row.TryGetValue("acq_date",  out var acqDate);
                    row.TryGetValue("acq_time",  out var acqTime);
                    row.TryGetValue("satellite", out var satellite);
                    row.TryGetValue("frp",       out var frpStr);
                    double.TryParse(frpStr, out var frp);

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
                        SourceUrl   = "https://firms.modaps.eosdis.nasa.gov/",
                        FetchedAt   = DateTime.UtcNow,
                        ExpiresAt   = DateTime.UtcNow.AddDays(8)
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

    // Parses the FIRMS CSV response into a list of header->value dictionaries.
    private static List<Dictionary<string, string>> ParseCsv(string csv)
    {
        var result = new List<Dictionary<string, string>>();
        var lines  = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return result;

        var headers = lines[0].Split(',');

        for (var i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Trim().Split(',');
            var row    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Length && j < values.Length; j++)
                row[headers[j].Trim()] = values[j].Trim();
            result.Add(row);
        }

        return result;
    }

    private static string MapFrpToSeverity(double frp) => frp switch
    {
        >= 200 => "CRITICAL",
        >= 50  => "HIGH",
        >= 10  => "MODERATE",
        _      => "LOW"
    };

    private static string FormatTime(string? hhmm)
    {
        if (hhmm is null || hhmm.Length < 4) return "";
        return $"{hhmm[..2]}:{hhmm[2..]}";
    }
}
