using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MichiMap.Api.Models;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;

namespace MichiMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubmissionsController(
    IEventRepository repo,
    MichiganCountyService counties,
    IBlobStorageService blob) : ControllerBase
{
    private static readonly HashSet<string> AllowedPhotoTypes =
        ["image/jpeg", "image/png", "image/webp"];

    private const long MaxPhotoBytes = 5 * 1024 * 1024; // 5 MB

    [HttpPost("morel")]
    [EnableRateLimiting("submissions")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitMorel([FromForm] MorelSubmission submission)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Validate county against Michigan county list
        var countyInfo = counties.Lookup(submission.County);
        if (countyInfo is null)
            return BadRequest(new { error = $"'{submission.County}' is not a recognized Michigan county." });

        // Validate observation date
        if (submission.ObservedDate > DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { error = "Observation date cannot be in the future." });
        if (submission.ObservedDate < DateOnly.FromDateTime(DateTime.Today.AddDays(-180)))
            return BadRequest(new { error = "Observation date is too far in the past." });

        // Validate and upload photo if provided
        string? photoUrl = null;
        if (submission.Photo is not null)
        {
            if (!AllowedPhotoTypes.Contains(submission.Photo.ContentType))
                return BadRequest(new { error = "Photo must be JPEG, PNG, or WebP." });

            if (submission.Photo.Length > MaxPhotoBytes)
                return BadRequest(new { error = "Photo must be 5 MB or smaller." });

            var blobName = $"{Guid.NewGuid()}{Path.GetExtension(submission.Photo.FileName)}";
            photoUrl = await blob.UploadPhotoAsync(submission.Photo, blobName);
        }

        // Snap location to county centroid
        var evt = new NaturalEvent
        {
            EventType = "MOREL",
            Title = $"Morel sighting — {submission.County} County",
            Description = submission.Notes,
            Lat = countyInfo.Lat,
            Lng = countyInfo.Lng,
            CountyFips = countyInfo.Fips,
            PhotoUrl = photoUrl,
            FetchedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await repo.UpsertEventAsync(evt);
        return CreatedAtAction("GetEvent", "Events", new { id = evt.EventId }, new { id = evt.EventId });
    }

    [HttpGet("counties")]
    public IActionResult GetCounties() => Ok(counties.AllCountyNames);
}
