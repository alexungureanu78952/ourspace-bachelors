using OurSpace.Profiles.Domain;

namespace OurSpace.Profiles.Application;

public sealed record ProfileResponse(Guid Id, Guid OwnerId, string DisplayName, DateTimeOffset CreatedAt);

public sealed class ProfileOperations(IProfileStore store, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<ProfileResponse> CreateAsync(string displayName, CancellationToken cancellationToken)
    {
        var ownerId = RequireUser();
        var profile = Profile.Create(Guid.NewGuid(), ownerId, displayName, clock.GetUtcNow());
        await store.AddAsync(profile, cancellationToken);
        return ToResponse(profile);
    }

    public async Task<ProfileResponse?> GetAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var ownerId = RequireUser();
        var profile = await store.FindAsync(profileId, cancellationToken);
        return profile is not null && profile.IsOwnedBy(ownerId) ? ToResponse(profile) : null;
    }

    private Guid RequireUser() =>
        currentUser.UserId is { } id && id != Guid.Empty
            ? id
            : throw new UnauthorizedAccessException("An authenticated user with a stable identifier is required.");

    private static ProfileResponse ToResponse(Profile profile) =>
        new(profile.Id, profile.OwnerId, profile.DisplayName, profile.CreatedAt);
}
