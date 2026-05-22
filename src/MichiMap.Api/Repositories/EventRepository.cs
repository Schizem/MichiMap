using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MichiMap.Api.Models;

namespace MichiMap.Api.Repositories;

public class EventRepository(MichiMapDbContext db) : IEventRepository
{
    public async Task<IEnumerable<NaturalEvent>> GetActiveEventsAsync(string? eventType = null, string? countyFips = null)
    {
        var query = db.NaturalEvents
            .Where(e => e.ExpiresAt == null || e.ExpiresAt > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(e => e.EventType == eventType.ToUpperInvariant());

        if (!string.IsNullOrEmpty(countyFips))
            query = query.Where(e => e.CountyFips == countyFips);

        return await query.AsNoTracking().ToListAsync();
    }

    public async Task UpsertEventAsync(NaturalEvent naturalEvent)
    {
        var existing = await db.NaturalEvents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.EventId == naturalEvent.EventId);

        if (existing is null)
            db.NaturalEvents.Add(naturalEvent);
        else
        {
            db.Entry(existing).CurrentValues.SetValues(naturalEvent);
            existing.IsDeleted = false;
        }

        await db.SaveChangesAsync();
    }

    public async Task<NaturalEvent?> GetByIdAsync(Guid eventId) =>
        await db.NaturalEvents.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);

    public async Task<IEnumerable<CountySummary>> GetCountySummaryAsync(string? eventType = null)
    {
        var param = new SqlParameter("@EventType", (object?)eventType ?? DBNull.Value);
        return await db.Database
            .SqlQueryRaw<CountySummary>("EXEC dbo.usp_GetEventSummaryByCounty @EventType", param)
            .ToListAsync();
    }

    public async Task SoftDeleteExpiredAsync()
    {
        await db.NaturalEvents
            .Where(e => e.ExpiresAt != null && e.ExpiresAt < DateTime.UtcNow && !e.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsDeleted, true));
    }
}
