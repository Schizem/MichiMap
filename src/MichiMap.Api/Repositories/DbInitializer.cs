using MichiMap.Api.Models;

namespace MichiMap.Api.Repositories;

public static class DbInitializer
{
    public static async Task SeedAsync(MichiMapDbContext db)
    {
        if (db.NaturalEvents.Any())
            return;

        var now = DateTime.UtcNow;

        var events = new List<NaturalEvent>
        {
            // Flood warnings (NWS)
            new()
            {
                EventId     = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                EventType   = "FLOOD",
                Title       = "Flood Warning - Houghton County",
                Description = "The Sturgeon River near Chassell is forecast to rise above flood stage through Saturday morning.",
                Severity    = "HIGH",
                Lat         = 47.0396m,
                Lng         = -88.5495m,
                CountyFips  = "26061",
                SourceUrl   = "https://alerts.weather.gov/",
                FetchedAt   = now,
                ExpiresAt   = now.AddDays(2)
            },
            new()
            {
                EventId     = Guid.Parse("a1000000-0000-0000-0000-000000000002"),
                EventType   = "FLOOD",
                Title       = "Flood Advisory - Saginaw County",
                Description = "Minor flooding expected along the Tittabawassee River near Midland.",
                Severity    = "MODERATE",
                Lat         = 43.4197m,
                Lng         = -83.9508m,
                CountyFips  = "26145",
                SourceUrl   = "https://alerts.weather.gov/",
                FetchedAt   = now,
                ExpiresAt   = now.AddDays(1)
            },

            // Active wildfires (FIRMS/NIFC)
            new()
            {
                EventId     = Guid.Parse("b2000000-0000-0000-0000-000000000001"),
                EventType   = "WILDFIRE",
                Title       = "Duck Lake Fire - Luce County (2012)",
                Description = "Acres burned: 21,000. Contained 85%.",
                Severity    = "HIGH",
                Lat         = 46.5956m,
                Lng         = -85.4064m,
                CountyFips  = "26095",
                SourceUrl   = "https://www.nifc.gov/fire-information/active-fires",
                FetchedAt   = now,
                ExpiresAt   = new DateTime(2013, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EventYear   = 2012
            },

            // Air quality (EPA AirNow)
            new()
            {
                EventId     = Guid.Parse("c3000000-0000-0000-0000-000000000001"),
                EventType   = "AIR_QUALITY",
                Title       = "Air Quality Alert - Wayne County",
                Description = "Fine particle pollution (PM2.5) expected to reach Unhealthy for Sensitive Groups levels.",
                Severity    = "MODERATE",
                Lat         = 42.3314m,
                Lng         = -83.0458m,
                CountyFips  = "26163",
                SourceUrl   = "https://www.airnow.gov/",
                FetchedAt   = now,
                ExpiresAt   = now.AddDays(1)
            },


            // Fish sightings (Michigan DNR Fish Atlas)
            new()
            {
                EventId     = Guid.Parse("e5000000-0000-0000-0000-000000000001"),
                EventType   = "FISH_STOCK",
                Title       = "Brook Trout - Boardman River",
                Description = "Scientific name: Salvelinus fontinalis | Observed: 2023",
                Severity    = null,
                Lat         = 44.7631m,
                Lng         = -85.6206m,
                CountyFips  = "26055",
                SourceUrl   = "https://gis-michigan.opendata.arcgis.com/datasets/Jdnp1TjADvSDxMAX::michigan-fish-atlas/about",
                FetchedAt   = now,
                ExpiresAt   = null
            },
            new()
            {
                EventId     = Guid.Parse("e5000000-0000-0000-0000-000000000002"),
                EventType   = "FISH_STOCK",
                Title       = "Chinook Salmon - Pere Marquette River",
                Description = "Scientific name: Oncorhynchus tshawytscha | Observed: 2023",
                Severity    = null,
                Lat         = 43.8867m,
                Lng         = -86.0467m,
                CountyFips  = "26105",
                SourceUrl   = "https://gis-michigan.opendata.arcgis.com/datasets/Jdnp1TjADvSDxMAX::michigan-fish-atlas/about",
                FetchedAt   = now,
                ExpiresAt   = null
            },

        };

        db.NaturalEvents.AddRange(events);
        await db.SaveChangesAsync();
    }
}
