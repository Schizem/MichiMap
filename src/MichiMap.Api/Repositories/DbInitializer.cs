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
