using Microsoft.EntityFrameworkCore;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using Xunit;

namespace MichiMap.Tests;

public class EventRepositoryTests
{
    private static MichiMapDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<MichiMapDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MichiMapDbContext(options);
    }

    [Fact]
    public async Task GetActiveEvents_ExcludesExpired()
    {
        using var db = CreateInMemoryDb();
        var repo = new EventRepository(db);

        await repo.UpsertEventAsync(new NaturalEvent
        {
            EventType = "FLOOD",
            Title = "Active flood",
            Lat = 43m, Lng = -84m,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });

        await repo.UpsertEventAsync(new NaturalEvent
        {
            EventType = "FLOOD",
            Title = "Expired flood",
            Lat = 43m, Lng = -84m,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });

        var results = await repo.GetActiveEventsAsync("FLOOD");

        Assert.Single(results);
        Assert.Equal("Active flood", results.First().Title);
    }

    [Fact]
    public async Task UpsertEvent_UpdatesExisting()
    {
        using var db = CreateInMemoryDb();
        var repo = new EventRepository(db);

        var eventId = Guid.NewGuid();
        await repo.UpsertEventAsync(new NaturalEvent { EventId = eventId, EventType = "WILDFIRE", Title = "Original", Lat = 44m, Lng = -85m });
        await repo.UpsertEventAsync(new NaturalEvent { EventId = eventId, EventType = "WILDFIRE", Title = "Updated", Lat = 44m, Lng = -85m });

        var results = await repo.GetActiveEventsAsync("WILDFIRE");
        Assert.Single(results);
        Assert.Equal("Updated", results.First().Title);
    }
}
