using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MichiMap.Api.Controllers;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using Xunit;

namespace MichiMap.Tests;

public class EventsControllerTests
{
    private static (EventsController controller, MichiMapDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<MichiMapDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MichiMapDbContext(options);
        var repo = new EventRepository(db);
        var controller = new EventsController(repo, new GeoJsonService());
        return (controller, db);
    }

    [Fact]
    public async Task GetEvents_EmptyDb_ReturnsEmptyFeatureCollection()
    {
        var (controller, _) = Build();

        var result = await controller.GetEvents(null, null) as OkObjectResult;

        Assert.NotNull(result);
        var body = result.Value!;
        Assert.Equal("FeatureCollection", body.GetType().GetProperty("type")!.GetValue(body));
    }

    [Fact]
    public async Task GetEvents_FilterByType_OnlyReturnsMatchingEvents()
    {
        var (controller, db) = Build();
        db.NaturalEvents.AddRange(
            new NaturalEvent { EventType = "FLOOD",    Title = "A", Lat = 43m, Lng = -84m },
            new NaturalEvent { EventType = "WILDFIRE", Title = "B", Lat = 44m, Lng = -85m }
        );
        await db.SaveChangesAsync();

        var result = await controller.GetEvents("FLOOD", null) as OkObjectResult;
        var features = (IEnumerable<object>)result!.Value!
            .GetType().GetProperty("features")!.GetValue(result.Value)!;

        Assert.Single(features);
    }

    [Fact]
    public async Task GetEvent_ExistingId_ReturnsEvent()
    {
        var (controller, db) = Build();
        var evt = new NaturalEvent { EventType = "MOREL", Title = "Test morel", Lat = 44m, Lng = -85m };
        db.NaturalEvents.Add(evt);
        await db.SaveChangesAsync();

        var result = await controller.GetEvent(evt.EventId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.IsType<NaturalEvent>(result.Value);
        Assert.Equal("Test morel", ((NaturalEvent)result.Value!).Title);
    }

    [Fact]
    public async Task GetEvent_MissingId_Returns404()
    {
        var (controller, _) = Build();

        var result = await controller.GetEvent(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }
}
