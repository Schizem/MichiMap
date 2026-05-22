using MichiMap.Api.Models;
using System.Text.Json;

namespace MichiMap.Api.Services;

public class GeoJsonService
{
    public object BuildFeatureCollection(IEnumerable<NaturalEvent> events)
    {
        var features = events.Select(e => new
        {
            type = "Feature",
            geometry = new
            {
                type = "Point",
                coordinates = new[] { (double)e.Lng, (double)e.Lat }
            },
            properties = new
            {
                eventId = e.EventId,
                eventType = e.EventType,
                title = e.Title,
                description = e.Description,
                severity = e.Severity,
                countyFips = e.CountyFips,
                sourceUrl = e.SourceUrl,
                fetchedAt = e.FetchedAt,
                expiresAt = e.ExpiresAt,
                photoUrl = e.PhotoUrl
            }
        });

        return new { type = "FeatureCollection", features };
    }
}
