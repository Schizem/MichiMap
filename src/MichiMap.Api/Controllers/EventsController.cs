using Microsoft.AspNetCore.Mvc;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;

namespace MichiMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController(IEventRepository repo, GeoJsonService geoJson) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? type,
        [FromQuery] string? county)
    {
        var events = await repo.GetActiveEventsAsync(type, county);
        return Ok(geoJson.BuildFeatureCollection(events));
    }

    [HttpGet("county-summary")]
    public async Task<IActionResult> GetCountySummary([FromQuery] string? type)
    {
        var summary = await repo.GetCountySummaryAsync(type);
        return Ok(summary);
    }
}
