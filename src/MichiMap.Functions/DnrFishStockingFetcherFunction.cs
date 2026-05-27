using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches Michigan DNR fish stocking events from the past 30 days, daily at 07:00 UTC.
// Service URL: verify current endpoint at https://gisopen.michigan.gov/arcgis/rest/services/DNR/
public class DnrFishStockingFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    MichiganCountyService counties,
    ILogger<DnrFishStockingFetcherFunction> logger)
{
    // Michigan Open Data — DNR fish stocking events (last 30 days)
    // URL subject to change; confirm at gisopen.michigan.gov before deploying
    private static string BuildUrl() =>
        "https://gisopen.michigan.gov/arcgis/rest/services/DNR/FishStocking/FeatureServer/0/query" +
        $"?where=STOCKING_DATE+>+'{DateTime.UtcNow.AddDays(-30):yyyy-MM-dd}'" +
        "&outFields=*&f=geojson";

    [Function("DnrFishStockingFetcher")]
    public async Task Run([TimerTrigger("0 0 7 * * *")] TimerInfo timer)
    {
        logger.LogInformation("DNR fish stocking fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("DNR fish stocking fetcher is running behind schedule");

        try
        {
            using var client = httpFactory.CreateClient();
            var response = await client.GetStringAsync(BuildUrl());
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

                var stockingId  = props.TryGetProperty("STOCKING_ID", out var sid) ? sid.GetString() : null;
                var waterbody   = props.TryGetProperty("WATERBODY",    out var wb)  ? wb.GetString()  : "Unknown waterbody";
                var countyName  = props.TryGetProperty("COUNTY",       out var cn)  ? cn.GetString()  : null;
                var species     = props.TryGetProperty("SPECIES",      out var sp)  ? sp.GetString()  : "Fish";
                var number      = props.TryGetProperty("NUMBER",       out var num) ? num.GetInt32()  : 0;
                var size        = props.TryGetProperty("SIZE",         out var sz)  ? sz.GetString()  : null;
                var stockDate   = props.TryGetProperty("STOCKING_DATE",out var sd)  ? sd.GetString()  : null;
                var countyInfo  = countyName is not null ? counties.Lookup(countyName) : null;

                var stableKey = stockingId ?? $"fishstock|{waterbody}|{stockDate}|{species}";

                var desc = number > 0
                    ? $"{number:N0} {species}{(size is not null ? $" ({size})" : "")} stocked"
                    : species;

                var evt = new NaturalEvent
                {
                    EventId     = EventNormalizer.StableGuid(stableKey),
                    EventType   = "FISH_STOCK",
                    Title       = $"{species} Stocked — {waterbody}",
                    Description = desc,
                    Severity    = null,
                    Lat         = lat,
                    Lng         = lng,
                    CountyFips  = countyInfo?.Fips,
                    SourceUrl   = "https://www.michigan.gov/dnr/managing-resources/fisheries/fish-stocking",
                    FetchedAt   = DateTime.UtcNow,
                    ExpiresAt   = DateTime.UtcNow.AddDays(30)
                };

                await repo.UpsertEventAsync(evt);
                upserted++;
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("DNR fish stocking fetch complete — {Count} event(s) upserted", upserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DNR fish stocking fetcher failed");
        }
    }
}
