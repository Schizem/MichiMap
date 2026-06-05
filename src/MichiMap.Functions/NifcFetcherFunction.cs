using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using MichiMap.Functions.Normalizers;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches current wildland fire incident locations for Michigan from NIFC/WFIGS.
// Dataset: https://data-nifc.opendata.arcgis.com/datasets/4181a117dc9e43db8598533e29972015_0
// Public endpoint - no API key required. All records are active/recent incidents.
public class NifcFetcherFunction(
    IEventRepository repo,
    IHttpClientFactory httpFactory,
    MichiganCountyService counties,
    ILogger<NifcFetcherFunction> logger)
{
    // WFIGS Current Wildland Fire Incident Locations - Michigan wildfires only.
    private const string NifcBaseUrl =
        "https://services3.arcgis.com/T4QMspbfLg3qTGWY/arcgis/rest/services/WFIGS_Incident_Locations_Current/FeatureServer/0/query" +
        "?where=POOState%3D'MI'%20AND%20IncidentTypeCategory%3D'WF'&outFields=*&outSR=4326&f=geojson";

    private const int PageSize = 1000;

    [Function("DnrWildfireFetcher")]
    public async Task Run([TimerTrigger("0 0 6 * * *")] TimerInfo timer)
    {
        logger.LogInformation("NIFC wildfire fetcher triggered at {Time}", DateTime.UtcNow);
        if (timer.IsPastDue)
            logger.LogWarning("NIFC wildfire fetcher is running behind schedule");

        try
        {
            using var client = httpFactory.CreateClient();
            var offset = 0;
            var total  = 0;

            while (true)
            {
                var url      = $"{NifcBaseUrl}&resultRecordCount={PageSize}&resultOffset={offset}";
                var response = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);

                var features = doc.RootElement.GetProperty("features").EnumerateArray().ToList();
                if (features.Count == 0) break;

                foreach (var feature in features)
                {
                    var props    = feature.GetProperty("properties");
                    var geometry = feature.GetProperty("geometry");

                    decimal lat, lng;
                    if (geometry.ValueKind != JsonValueKind.Null)
                    {
                        var coords = geometry.GetProperty("coordinates");
                        lng = coords[0].GetDecimal();
                        lat = coords[1].GetDecimal();
                    }
                    else
                    {
                        // Fall back to stored coordinate properties if geometry is absent.
                        if (!props.TryGetProperty("InitialLatitude",  out var latEl) || latEl.ValueKind == JsonValueKind.Null) continue;
                        if (!props.TryGetProperty("InitialLongitude", out var lngEl) || lngEl.ValueKind == JsonValueKind.Null) continue;
                        lat = latEl.GetDecimal();
                        lng = lngEl.GetDecimal();
                    }

                    if (lat == 0m && lng == 0m) continue;

                    var name      = props.TryGetProperty("IncidentName",          out var nm) ? nm.GetString() : null;
                    var acres     = props.TryGetProperty("FinalAcres",             out var ac) && ac.ValueKind == JsonValueKind.Number ? ac.GetDouble() : 0;
                    var pct       = props.TryGetProperty("PercentContained",       out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetDouble() : 0;
                    var countyRaw = props.TryGetProperty("POOCounty",              out var co) ? co.GetString() : null;
                    var discovered = props.TryGetProperty("FireDiscoveryDateTime", out var dd) && dd.ValueKind == JsonValueKind.Number
                                         ? DateTimeOffset.FromUnixTimeMilliseconds(dd.GetInt64()).UtcDateTime
                                         : (DateTime?)null;

                    var countyInfo = countyRaw is not null ? counties.Lookup(countyRaw) : null;
                    var stableKey  = $"nifc-wfigs|{name}|{discovered:yyyy-MM-dd}|{lat:F4}|{lng:F4}";

                    var descParts = new List<string>();
                    if (acres > 0)           descParts.Add($"{acres:N0} acres");
                    if (pct > 0)             descParts.Add($"{pct:N0}% contained");
                    if (discovered.HasValue) descParts.Add($"Discovered: {discovered:MMM d, yyyy}");

                    var evt = new NaturalEvent
                    {
                        EventId     = EventNormalizer.StableGuid(stableKey),
                        EventType   = "WILDFIRE",
                        Title       = name is not null ? $"Wildfire - {name}" : "Active Wildfire - Michigan",
                        Description = descParts.Count > 0 ? string.Join(" | ", descParts) : null,
                        Severity    = acres >= 1000 ? "CRITICAL" : acres >= 100 ? "HIGH" : "MODERATE",
                        Lat         = lat,
                        Lng         = lng,
                        CountyFips  = countyInfo?.Fips,
                        SourceUrl   = "https://data-nifc.opendata.arcgis.com/datasets/4181a117dc9e43db8598533e29972015_0",
                        FetchedAt   = DateTime.UtcNow,
                        ExpiresAt   = DateTime.UtcNow.AddDays(14),
                        EventYear   = null // Current active incidents - displayed as LIVE
                    };

                    await repo.UpsertEventAsync(evt);
                    total++;
                }

                offset += PageSize;
                if (features.Count < PageSize) break;
            }

            await repo.SoftDeleteExpiredAsync();
            logger.LogInformation("NIFC wildfire fetch complete - {Count} active incident(s) upserted", total);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NIFC wildfire fetcher failed");
        }
    }
}
