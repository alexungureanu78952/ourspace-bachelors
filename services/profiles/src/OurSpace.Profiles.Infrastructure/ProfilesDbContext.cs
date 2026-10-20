using Microsoft.EntityFrameworkCore;
using OurSpace.Profiles.Domain;

namespace OurSpace.Profiles.Infrastructure;

public sealed class ProfilesDbContext(DbContextOptions<ProfilesDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var profile = modelBuilder.Entity<Profile>();
        profile.ToTable("Profiles", table => table.HasCheckConstraint("CK_Profiles_DisplayName", "char_length(btrim(\"DisplayName\")) BETWEEN 1 AND 200"));
        profile.HasKey(p => p.Id);
        profile.Property(p => p.DisplayName).HasMaxLength(200).IsRequired();
        profile.HasIndex(p => p.OwnerId);

        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.ToTable("OutboxMessages");
        outbox.HasKey(m => m.EventId);
        outbox.Property(m => m.EventType).HasMaxLength(100);
        outbox.Property(m => m.Producer).HasMaxLength(100);
        outbox.Property(m => m.CorrelationId).HasMaxLength(100);
        outbox.Property(m => m.CausationId).HasMaxLength(100);
        outbox.Property(m => m.Payload).HasColumnType("jsonb");
        outbox.HasIndex(m => new { m.ProcessedAt, m.OccurredAt });
    }
}
