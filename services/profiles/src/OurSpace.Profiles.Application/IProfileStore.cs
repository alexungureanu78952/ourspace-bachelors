using OurSpace.Profiles.Domain;

namespace OurSpace.Profiles.Application;

public interface IProfileStore
{
    Task AddAsync(Profile profile, CancellationToken cancellationToken);
    Task<Profile?> FindAsync(Guid profileId, CancellationToken cancellationToken);
}
