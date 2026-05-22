using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using System.Text.Json;

namespace MichiMap.Functions;

// Fetches active wildfire perimeters in Michigan from NIFC ArcGIS REST every hour.
public class NifcFetcherFunction(IEventRepository repo, IHttpClientFactory httpFactory, ILogger<NifcFetcherFunction> logger)
{
    private const string NifcUrl =
        "https://services3.arcgis.com/T4QMspbfLg3qTGWY/arcgis/rest/services/Active_Fires/FeatureServer/0/query" +
        "?where=POOState%3D'US-MI'&outFields=*&f=geojson";

    [Function("NifcFetcher")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("NIFC fetcher triggered at {Time}", DateTime.UtcNow);

        using var client = httpFactory.CreateClient();
        var response = await client.GetStringAsync(NifcUrl);
        using var doc = JsonDocument.Parse(response);

        foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            var props = feature.GetProperty("properties");
            var geometry = feature.GetProperty("geometry");
            if (geometry.ValueKind == JsonValueKind.Null) continue;

            var coords = geometry.GetProperty("coordinates");
            var lng = coords[0].GetDecimal();
            var lat = coords[1].GetDecimal();

            var globalId = props.GetProperty("GlobalID").GetString() ?? Guid.NewGuid().ToString();

            var evt = new NaturalEvent
            {
                EventId = Guid.Parse(globalId.Trim('{', '}')),
                EventType = "WILDFIRE",
                Title = props.GetProperty("IncidentName").GetString() ?? "Active Wildfire",
                Description = $"Acres burned: {props.GetProperty("DailyAcres").GetDouble():N0}",
                Severity = "HIGH",
                Lat = lat,
                Lng = lng,
                SourceUrl = "https://www.nifc.gov/fire-information/active-fires",
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(2)
            };

            await repo.UpsertEventAsync(evt);
        }

        await repo.SoftDeleteExpiredAsync();
        logger.LogInformation("NIFC fetch complete");
    }
}
