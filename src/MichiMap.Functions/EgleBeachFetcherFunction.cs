using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches Michigan beach closures and advisories from EGLE BeachGuard daily at 08:00 UTC.
// Service URL: verify current endpoint at https://gisopen.michigan.gov/arcgis/rest/services/DEQ/
public class EgleBeachFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    MichiganCountyService counties,
    ILogger<EgleBeachFetcherFunction> logger)
{
    // Michigan EGLE (formerly DEQ) BeachGuard — closed and advisory beaches
    // URL subject to change; confirm at gisopen.michigan.gov before deploying
    private const string BeachUrl =
        "https://gisopen.michigan.gov/arcgis/rest/services/DEQ/BeachGuard/FeatureServer/0/query" +
        "?where=STATUS+IN+('Closed','Advisory')&outFields=*&f=geojson";

    [Function("EgleBeachFetcher")]
    public async Task Run([TimerTrigger("0 0 8 * * *")] TimerInfo timer)
    {
        logger.LogInformation("EGLE beach fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("EGLE beach fetcher is running behind schedule");

        try
        {
            using var client = httpFactory.CreateClient();
            var response = await client.GetStringAsync(BeachUrl);
            using var doc = JsonDocument.Parse(response);

            var upserted = 0;
            foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var props    = feature.GetProperty("properties");
                var geometry = feature.GetProperty("geometry");
                if (geometry.ValueKind == JsonValueKind.Null) continue;

                var coords = geometry.GetProperty("coordinates");
                var lat    = coords[1].GetDecimal();
                var lng    = coords[0].GetDecimal();

                var beachId    = props.TryGetProperty("BEACH_ID",         out var bid) ? bid.GetString() : null;
                var beachName  = props.TryGetProperty("BEACH_NAME",       out var bn)  ? bn.GetString()  : "Unknown Beach";
                var status     = props.TryGetProperty("STATUS",           out var st)  ? st.GetString()  : "Closed";
                var reason     = props.TryGetProperty("REASON",           out var r)   ? r.GetString()   : null;
                var countyName = props.TryGetProperty("COUNTY",           out var cn)  ? cn.GetString()  : null;
                var sampleDate = props.TryGetProperty("LAST_SAMPLE_DATE", out var sd)  ? sd.GetString()  : null;
                var countyInfo = countyName is not null ? counties.Lookup(countyName) : null;

                var stableKey  = beachId ?? $"beach|{beachName}|{sampleDate}";
                var isClosed   = string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);

                var evt = new NaturalEvent
                {
                    EventId     = EventNormalizer.StableGuid(stableKey),
                    EventType   = "BEACH",
                    Title       = $"Beach {status} — {beachName}",
                    Description = reason,
                    Severity    = isClosed ? "HIGH" : "MODERATE",
                    Lat         = lat,
                    Lng         = lng,
                    CountyFips  = countyInfo?.Fips,
                    SourceUrl   = "https://www.michigan.gov/egle/about/organization/water-resources/beach-guard",
                    FetchedAt   = DateTime.UtcNow,
                    ExpiresAt   = DateTime.UtcNow.AddDays(3)
                };

                await repo.UpsertEventAsync(evt);
                upserted++;
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("EGLE beach fetch complete — {Count} closure(s) upserted", upserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "EGLE beach fetcher failed");
        }
    }
}
