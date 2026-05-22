namespace MichiMap.Api.Models;

public class NaturalEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public required string EventType { get; set; }   // FLOOD, WILDFIRE, BURN, AIR_QUALITY, BEACH, FISH_STOCK, MOREL
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Severity { get; set; } // LOW, MODERATE, HIGH, CRITICAL
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public string? CountyFips { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public string? PhotoUrl { get; set; } // morel submissions only
    public bool IsDeleted { get; set; } = false;
}
