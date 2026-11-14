using OurSpace.Profiles.Domain;

namespace OurSpace.Profiles.Application;

public interface IProfileStore
{
    Task AddAsync(Profile profile, CancellationToken cancellationToken);
    Task<Profile?> FindAsync(Guid profileId, CancellationToken cancellationToken);
    Task<Profile?> FindByOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
    Task UpdateAsync(Profile profile, CancellationToken cancellationToken);
}
