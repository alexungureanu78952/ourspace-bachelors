using OurSpace.Profiles.Application;
using OurSpace.Profiles.Domain;
using Xunit;

namespace OurSpace.Profiles.UnitTests;

public sealed class ProfileOperationsTests
{
    [Fact]
    public async Task CreateUsesCurrentUserAndNormalizesName()
    {
        var owner = Guid.NewGuid();
        var store = new MemoryStore();
        var operations = new ProfileOperations(store, new FakeUser(owner), TimeProvider.System);
        var result = await operations.CreateAsync("  Alex  ", string.Empty, CancellationToken.None);
        Assert.Equal(owner, result.OwnerId);
        Assert.Equal("Alex", result.DisplayName);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task AnotherUserCannotReadPrivateProfile()
    {
        var owner = Guid.NewGuid();
        var store = new MemoryStore();
        var creator = new ProfileOperations(store, new FakeUser(owner), TimeProvider.System);
        var profile = await creator.CreateAsync("Alex", string.Empty, CancellationToken.None);
        var reader = new ProfileOperations(store, new FakeUser(Guid.NewGuid()), TimeProvider.System);
        Assert.Null(await reader.GetAsync(profile.Id, CancellationToken.None));
        Assert.NotNull(await creator.GetAsync(profile.Id, CancellationToken.None));
    }

    [Fact]
    public async Task AnonymousOperationsFailBeforeStorageIsTouched()
    {
        var store = new MemoryStore();
        var operations = new ProfileOperations(store, new FakeUser(null), TimeProvider.System);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => operations.CreateAsync("Alex", string.Empty, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => operations.GetAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => operations.GetMyAsync(CancellationToken.None));
        Assert.Equal(0, store.ReadCount);
        Assert.Empty(store.Items);
    }

    [Fact]
    public async Task UpdateChangesNameAndBio()
    {
        var owner = Guid.NewGuid();
        var store = new MemoryStore();
        var operations = new ProfileOperations(store, new FakeUser(owner), TimeProvider.System);
        await operations.CreateAsync("Original", "old bio", CancellationToken.None);

        var result = await operations.UpdateMyAsync("  Updated  ", "new bio", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Updated", result.DisplayName);
        Assert.Equal("new bio", result.Bio);
    }

    [Fact]
    public async Task UpdateReturnsNullWhenProfileDoesNotExist()
    {
        var store = new MemoryStore();
        var operations = new ProfileOperations(store, new FakeUser(Guid.NewGuid()), TimeProvider.System);

        var result = await operations.UpdateMyAsync("Name", string.Empty, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, store.UpdateCount);
    }

    [Fact]
    public async Task UpdateRejectsInvalidName()
    {
        var owner = Guid.NewGuid();
        var store = new MemoryStore();
        var operations = new ProfileOperations(store, new FakeUser(owner), TimeProvider.System);
        await operations.CreateAsync("Original", string.Empty, CancellationToken.None);

        await Assert.ThrowsAsync<ProfileValidationException>(() =>
            operations.UpdateMyAsync(string.Empty, string.Empty, CancellationToken.None));
        Assert.Equal(0, store.UpdateCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Alex\n")]
    public void InvalidNameIsRejected(string name)
    {
        // Embedded control characters are rejected even if whitespace normalization would remove them.
        Assert.Throws<ProfileValidationException>(() => Profile.Create(Guid.NewGuid(), Guid.NewGuid(), name, string.Empty, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void LengthAndIdentityBoundariesAreEnforced()
    {
        Assert.Throws<ProfileValidationException>(() => Profile.Create(Guid.NewGuid(), Guid.NewGuid(), new string('a', 201), string.Empty, DateTimeOffset.UtcNow));
        Assert.Equal(200, Profile.Create(Guid.NewGuid(), Guid.NewGuid(), new string('a', 200), string.Empty, DateTimeOffset.UtcNow).DisplayName.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile.Create(Guid.Empty, Guid.NewGuid(), "Alex", string.Empty, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile.Create(Guid.NewGuid(), Guid.Empty, "Alex", string.Empty, DateTimeOffset.UtcNow));
    }

    private sealed record FakeUser(Guid? UserId) : ICurrentUser;

    private sealed class MemoryStore : IProfileStore
    {
        public List<Profile> Items { get; } = [];
        public int ReadCount { get; private set; }
        public int UpdateCount { get; private set; }

        public Task AddAsync(Profile profile, CancellationToken cancellationToken)
        {
            Items.Add(profile);
            return Task.CompletedTask;
        }

        public Task<Profile?> FindAsync(Guid profileId, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Items.SingleOrDefault(profile => profile.Id == profileId));
        }

        public Task<Profile?> FindByOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Items.SingleOrDefault(profile => profile.OwnerId == ownerId));
        }

        public Task UpdateAsync(Profile profile, CancellationToken cancellationToken)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }
    }
}
