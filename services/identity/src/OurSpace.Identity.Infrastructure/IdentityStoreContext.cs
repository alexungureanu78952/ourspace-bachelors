using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OurSpace.Identity.Infrastructure;

public sealed class IdentityStoreContext(DbContextOptions<IdentityStoreContext> options)
    : IdentityDbContext<IdentityUserAccount, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseOpenIddict();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<IdentityUserAccount>().HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
