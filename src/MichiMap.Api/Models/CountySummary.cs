namespace MichiMap.Api.Models;

// Result type for usp_GetEventSummaryByCounty stored procedure
public class CountySummary
{
    public string? CountyFips { get; set; }
    public string? EventType { get; set; }
    public int EventCount { get; set; }
    public DateTime LatestFetchedAt { get; set; }
    public int MaxSeverityRank { get; set; }
}
