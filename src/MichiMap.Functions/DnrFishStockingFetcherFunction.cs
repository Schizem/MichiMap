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
    // Limit to observations from 2020 onward to keep the dataset manageable.
    // outSR=4326 returns coordinates in lat/lng. resultRecordCount caps the response
    // at 1000 features per run so the fetcher stays within a reasonable payload size.
    private const string FishUrl =
        "https://services3.arcgis.com/Jdnp1TjADvSDxMAX/arcgis/rest/services/DNRFisheriesDataOPENDATA/FeatureServer/0/query" +
        "?where=Year%3E%3D2020&outFields=*&outSR=4326&resultRecordCount=1000&f=geojson";

    [Function("DnrFishAtlasFetcher")]
    public async Task Run([TimerTrigger("0 0 7 * * *")] TimerInfo timer)
    {
        logger.LogInformation("DNR Fish Atlas fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("DNR Fish Atlas fetcher is running behind schedule");

        try
        {
            using var client = httpFactory.CreateClient();
            var response = await client.GetStringAsync(FishUrl);
            using var doc = JsonDocument.Parse(response);

            var upserted = 0;
            foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var props    = feature.GetProperty("properties");
                var geometry = feature.GetProperty("geometry");
                if (geometry.ValueKind == JsonValueKind.Null) continue;

                var coords = geometry.GetProperty("coordinates");
                var lng    = coords[0].GetDecimal();
                var lat    = coords[1].GetDecimal();
                if (lat == 0m && lng == 0m) continue;

                var globalId    = props.TryGetProperty("GlobalID",   out var gid) ? gid.GetString()  : null;
                var commonName  = props.TryGetProperty("CommonName", out var cn)  ? cn.GetString()   : "Unknown Species";
                var taxon       = props.TryGetProperty("Taxon",      out var tx)  ? tx.GetString()   : null;
                var location    = props.TryGetProperty("Location",   out var loc) ? loc.GetString()  : null;
                var countyRaw   = props.TryGetProperty("County",     out var co)  ? co.GetString()   : null;
                var year        = props.TryGetProperty("Year",       out var yr)  ? yr.GetInt32()    : 0;

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

                var evt = new NaturalEvent
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
                    ExpiresAt   = null
                };

                await repo.UpsertEventAsync(evt);
                upserted++;
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("DNR Fish Atlas fetch complete - {Count} record(s) upserted", upserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DNR Fish Atlas fetcher failed");
        }
    }
}
