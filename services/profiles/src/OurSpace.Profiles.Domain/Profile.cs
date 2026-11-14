namespace OurSpace.Profiles.Domain;

public sealed class Profile
{
    private Profile() { }

    private Profile(Guid id, Guid ownerId, string displayName, string bio, DateTimeOffset createdAt)
    {
        Id = id;
        OwnerId = ownerId;
        DisplayName = displayName;
        Bio = bio;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Bio { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsArchived { get; private set; }

    public static Profile Create(Guid id, Guid ownerId, string displayName, string bio, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(ownerId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(displayName);
        var normalized = displayName.Trim();
        if (normalized.Length is 0 or > 200 || displayName.Any(char.IsControl))
        {
            throw new ProfileValidationException("Display name must contain 1–200 characters without control characters.");
        }

        ArgumentNullException.ThrowIfNull(bio);
        var normalizedBio = bio.Trim();
        if (normalizedBio.Length > 500 || bio.Any(c => char.IsControl(c) && c != '\n' && c != '\r'))
        {
            throw new ProfileValidationException("Bio must be at most 500 characters without control characters.");
        }

        return new Profile(id, ownerId, normalized, normalizedBio, createdAt.ToUniversalTime());
    }

    public void Update(string displayName, string bio)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        var normalized = displayName.Trim();
        if (normalized.Length is 0 or > 200 || displayName.Any(char.IsControl))
        {
            throw new ProfileValidationException("Display name must contain 1–200 characters without control characters.");
        }

        ArgumentNullException.ThrowIfNull(bio);
        var normalizedBio = bio.Trim();
        if (normalizedBio.Length > 500 || bio.Any(c => char.IsControl(c) && c != '\n' && c != '\r'))
        {
            throw new ProfileValidationException("Bio must be at most 500 characters without control characters.");
        }

        DisplayName = normalized;
        Bio = normalizedBio;
    }

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;
}
