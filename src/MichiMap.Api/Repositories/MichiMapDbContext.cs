using Microsoft.EntityFrameworkCore;
using MichiMap.Api.Models;

namespace MichiMap.Api.Repositories;

public class MichiMapDbContext(DbContextOptions<MichiMapDbContext> options) : DbContext(options)
{
    public DbSet<NaturalEvent> NaturalEvents => Set<NaturalEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NaturalEvent>(e =>
        {
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventType).HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(255);
            e.Property(x => x.Severity).HasMaxLength(10);
            e.Property(x => x.CountyFips).HasMaxLength(5);
            e.Property(x => x.Lat).HasColumnType("decimal(9,6)");
            e.Property(x => x.Lng).HasColumnType("decimal(9,6)");
            e.HasIndex(x => x.EventType);
            e.HasIndex(x => x.ExpiresAt);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}
