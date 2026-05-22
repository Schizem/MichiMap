using MichiMap.Api.Models;

namespace MichiMap.Api.Repositories;

public interface IEventRepository
{
    Task<IEnumerable<NaturalEvent>> GetActiveEventsAsync(string? eventType = null, string? countyFips = null);
    Task<NaturalEvent?> GetByIdAsync(Guid eventId);
    Task<IEnumerable<CountySummary>> GetCountySummaryAsync(string? eventType = null);
    Task UpsertEventAsync(NaturalEvent naturalEvent);
    Task SoftDeleteExpiredAsync();
}
