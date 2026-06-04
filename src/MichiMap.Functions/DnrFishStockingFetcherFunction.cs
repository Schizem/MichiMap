using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches confirmed fish species observations from the Michigan DNR Fish Atlas.
// The Fish Atlas records verified species presence across Michigan waterbodies.
// Endpoint verified 2026-06-04:
// https://services3.arcgis.com/Jdnp1TjADvSDxMAX/arcgis/rest/services/DNRFisheriesDataOPENDATA/FeatureServer/0
public class DnrFishStockingFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    MichiganCountyService counties,
    ILogger<DnrFishStockingFetcherFunction> logger)
{
    private const string FishBaseUrl =
        "https://services3.arcgis.com/Jdnp1TjADvSDxMAX/arcgis/rest/services/DNRFisheriesDataOPENDATA/FeatureServer/0/query" +
        "?where=Year%3E%3D2020&outFields=*&outSR=4326&f=geojson";

    // ArcGIS FeatureServer page size - stay at or below the service's maxRecordCount.
    private const int PageSize = 1000;

    [Function("DnrFishAtlasFetcher")]
    public async Task Run([TimerTrigger("0 0 7 * * *")] TimerInfo timer)
    {
        logger.LogInformation("DNR Fish Atlas fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("DNR Fish Atlas fetcher is running behind schedule");

        try
        {
            using var client = httpFactory.CreateClient();
            var batch    = new List<NaturalEvent>();
            var offset   = 0;
            var total    = 0;

            while (true)
            {
                var url      = $"{FishBaseUrl}&resultRecordCount={PageSize}&resultOffset={offset}";
                var response = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);

                var features = doc.RootElement.GetProperty("features").EnumerateArray().ToList();
                if (features.Count == 0) break;

                foreach (var feature in features)
                {
                    var props    = feature.GetProperty("properties");
                    var geometry = feature.GetProperty("geometry");
                    if (geometry.ValueKind == JsonValueKind.Null) continue;

                    var coords = geometry.GetProperty("coordinates");
                    var lng    = coords[0].GetDecimal();
                    var lat    = coords[1].GetDecimal();
                    if (lat == 0m && lng == 0m) continue;

                    var globalId   = props.TryGetProperty("GlobalID",   out var gid) ? gid.GetString()  : null;
                    var commonName = props.TryGetProperty("CommonName", out var cn)  ? cn.GetString()   : "Unknown Species";
                    var taxon      = props.TryGetProperty("Taxon",      out var tx)  ? tx.GetString()   : null;
                    var location   = props.TryGetProperty("Location",   out var loc) ? loc.GetString()  : null;
                    var countyRaw  = props.TryGetProperty("County",     out var co)  ? co.GetString()   : null;
                    var year       = props.TryGetProperty("Year",       out var yr)  ? yr.GetInt32()    : 0;

                    // County field may be blank in the Fish Atlas; fall back gracefully.
                    var countyName = !string.IsNullOrWhiteSpace(countyRaw)
                        ? countyRaw.Replace(" County", "", StringComparison.OrdinalIgnoreCase).Trim()
                        : null;
                    var countyInfo = countyName is not null ? counties.Lookup(countyName) : null;

                    // GlobalID is a stable unique identifier supplied by the DNR.
                    var stableKey = globalId ?? $"fishsighting|{taxon}|{location}|{year}|{lat:F4}|{lng:F4}";

                    var title = location is not null
                        ? $"{commonName} - {location}"
                        : commonName ?? "Fish Sighting";

                    var desc = taxon is not null
                        ? $"Scientific name: {taxon}{(year > 0 ? $" | Observed: {year}" : "")}"
                        : year > 0 ? $"Observed: {year}" : null;

                    batch.Add(new NaturalEvent
                    {
                        EventId     = EventNormalizer.StableGuid(stableKey),
                        EventType   = "FISH_STOCK",
                        Title       = title,
                        Description = desc,
                        Severity    = null,
                        Lat         = lat,
                        Lng         = lng,
                        CountyFips  = countyInfo?.Fips,
                        SourceUrl   = "https://gis-michigan.opendata.arcgis.com/datasets/Jdnp1TjADvSDxMAX::michigan-fish-atlas/about",
                        FetchedAt   = DateTime.UtcNow,
                        ExpiresAt   = null,
                        EventYear   = year > 0 ? year : null
                    });
                }

                // Upsert the page before fetching the next to keep memory bounded.
                foreach (var evt in batch)
                    await repo.UpsertEventAsync(evt);

                total  += batch.Count;
                offset += PageSize;
                batch.Clear();

                // Fewer records than a full page means we've reached the last page.
                if (features.Count < PageSize) break;
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("DNR Fish Atlas fetch complete - {Count} record(s) upserted", total);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DNR Fish Atlas fetcher failed");
        }
    }
}
