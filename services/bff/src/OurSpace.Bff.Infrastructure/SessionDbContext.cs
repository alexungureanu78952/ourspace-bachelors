using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OurSpace.Bff.Infrastructure;

public sealed class SessionRecord
{
    public string Id { get; set; } = string.Empty;
    public byte[] Payload { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class SessionDbContext(DbContextOptions<SessionDbContext> options) : DbContext(options)
{
    public DbSet<SessionRecord> Sessions => Set<SessionRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionRecord>().ToTable("BffSessions").HasKey(session => session.Id);
        modelBuilder.Entity<SessionRecord>().Property(session => session.Id).HasMaxLength(64);
        modelBuilder.Entity<SessionRecord>().HasIndex(session => session.ExpiresAt);
    }
}

public sealed class SessionDesignTimeFactory : IDesignTimeDbContextFactory<SessionDbContext>
{
    public SessionDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SessionDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Bff") ?? "Host=127.0.0.1;Database=ourspace_bff;Username=ourspace_bff").Options);
}
