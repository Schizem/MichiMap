using Microsoft.AspNetCore.Mvc;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;

namespace MichiMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController(IEventRepository repo, GeoJsonService geoJson) : ControllerBase
{
    // GET /api/events?type=FLOOD&county=26061
    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? type,
        [FromQuery] string? county)
    {
        var events = await repo.GetActiveEventsAsync(type, county);
        return Ok(geoJson.BuildFeatureCollection(events));
    }

    // GET /api/events/{id} used by the sidebar detail panel
    [HttpGet("{id:guid}", Name = "GetEvent")]
    public async Task<IActionResult> GetEvent(Guid id)
    {
        var evt = await repo.GetByIdAsync(id);
        return evt is null ? NotFound() : Ok(evt);
    }

    // GET /api/events/county-summary?type=FLOOD
    [HttpGet("county-summary")]
    public async Task<IActionResult> GetCountySummary([FromQuery] string? type)
    {
        var summary = await repo.GetCountySummaryAsync(type);
        return Ok(summary);
    }
}
