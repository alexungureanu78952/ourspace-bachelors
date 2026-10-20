namespace OurSpace.Profiles.Domain;

public sealed class Profile
{
    private Profile() { }

    private Profile(Guid id, Guid ownerId, string displayName, DateTimeOffset createdAt)
    {
        Id = id;
        OwnerId = ownerId;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Profile Create(Guid id, Guid ownerId, string displayName, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(ownerId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(displayName);
        var normalized = displayName.Trim();
        if (normalized.Length is 0 or > 200 || displayName.Any(char.IsControl))
        {
            throw new ProfileValidationException("Display name must contain 1–200 characters without control characters.");
        }

        return new Profile(id, ownerId, normalized, createdAt.ToUniversalTime());
    }

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;
}
