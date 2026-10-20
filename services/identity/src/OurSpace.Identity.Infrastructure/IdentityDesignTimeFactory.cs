using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OurSpace.Identity.Infrastructure;

public sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityStoreContext>
{
    public IdentityStoreContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Identity")
            ?? "Host=127.0.0.1;Database=ourspace_identity;Username=ourspace_identity";
        return new IdentityStoreContext(new DbContextOptionsBuilder<IdentityStoreContext>().UseNpgsql(connection).UseOpenIddict().Options);
    }
}
