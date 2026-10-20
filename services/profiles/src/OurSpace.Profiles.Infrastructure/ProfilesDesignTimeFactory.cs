using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OurSpace.Profiles.Infrastructure;

public sealed class ProfilesDesignTimeFactory : IDesignTimeDbContextFactory<ProfilesDbContext>
{
    public ProfilesDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Profiles")
            ?? "Host=127.0.0.1;Database=ourspace_profiles;Username=ourspace_profiles";
        return new ProfilesDbContext(new DbContextOptionsBuilder<ProfilesDbContext>().UseNpgsql(connection).Options);
    }
}
