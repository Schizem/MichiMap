using Microsoft.AspNetCore.Mvc;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;

namespace MichiMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubmissionsController(IEventRepository repo) : ControllerBase
{
    [HttpPost("morel")]
    public async Task<IActionResult> SubmitMorel([FromForm] MorelSubmission submission)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var naturalEvent = new NaturalEvent
        {
            EventType = "MOREL",
            Title = $"Morel sighting — {submission.County} County",
            Description = submission.Notes,
            // Lat/Lng will be resolved to township centroid from CountyFips placeholder
            Lat = 0,
            Lng = 0,
            CountyFips = submission.County,
            FetchedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await repo.UpsertEventAsync(naturalEvent);
        return CreatedAtAction(nameof(EventsController.GetEvents), "Events", null, new { id = naturalEvent.EventId });
    }
}
