using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OurSpace.Identity.Infrastructure;
using OurSpace.Profiles.Application;
using OurSpace.Profiles.Domain;
using OurSpace.Profiles.Infrastructure;
using Xunit;

namespace OurSpace.Backend.IntegrationTests;

public sealed class BackendTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task OwnerCanCreateAndRetrieveButAnotherUserCannot()
    {
        await using var factory = new ProfileApiFactory(postgres.ProfilesConnection);
        using var client = factory.CreateClient();
        var owner = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Test-User", owner.ToString());
        using var created = await client.PostAsJsonAsync("/api/v1/profiles/", new { displayName = "  Alex  " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var profile = await created.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(owner, profile.OwnerId);
        Assert.Equal("Alex", profile.DisplayName);
        using var owned = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var privateProfile = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.NotFound, privateProfile.StatusCode);
        using var absent = await client.GetAsync("/api/v1/profiles/" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);

        await using var database = new ProfilesDbContext(new DbContextOptionsBuilder<ProfilesDbContext>().UseNpgsql(postgres.ProfilesConnection).Options);
        Assert.Equal(owner, (await database.Profiles.SingleAsync(p => p.Id == profile.Id)).OwnerId);
        Assert.Single(await database.OutboxMessages.Where(m => m.AggregateId == profile.Id).ToListAsync());
    }

    [Fact]
    public async Task FreshHostReadsPersistedProfile()
    {
        Guid id;
        var owner = Guid.NewGuid().ToString();
        await using (var factory = new ProfileApiFactory(postgres.ProfilesConnection))
        {
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-User", owner);
            using var created = await client.PostAsJsonAsync("/api/v1/profiles/", new { displayName = "Persisted" });
            var profile = await created.Content.ReadFromJsonAsync<ProfileResponse>();
            Assert.NotNull(profile);
            id = profile.Id;
        }

        await using var secondFactory = new ProfileApiFactory(postgres.ProfilesConnection);
        using var reader = secondFactory.CreateClient();
        reader.DefaultRequestHeaders.Add("X-Test-User", owner);
        Assert.Equal("Persisted", (await reader.GetFromJsonAsync<ProfileResponse>("/api/v1/profiles/" + id))?.DisplayName);
    }

    [Fact]
    public async Task AnonymousAndMalformedIdentitiesAreRejected()
    {
        await using var factory = new ProfileApiFactory(postgres.ProfilesConnection);
        using var client = factory.CreateClient();
        using var anonymous = await client.PostAsJsonAsync("/api/v1/profiles/", new { displayName = "Alex" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-User", "not-a-stable-id");
        using var malformed = await client.GetAsync("/api/v1/profiles/" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, malformed.StatusCode);
    }

    [Fact]
    public async Task ProductionAuthenticationIgnoresTestHeader()
    {
        await using var factory = new ProfileApiFactory(postgres.ProfilesConnection, fakeAuthentication: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.PostAsJsonAsync("/api/v1/profiles/", new { displayName = "Alex" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClientCannotChooseOwnerAndInvalidNamesAreRejected()
    {
        await using var factory = new ProfileApiFactory(postgres.ProfilesConnection);
        using var client = factory.CreateClient();
        var owner = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Test-User", owner.ToString());
        using var created = await client.PostAsJsonAsync("/api/v1/profiles/", new { displayName = "Alex", ownerId = Guid.NewGuid() });
        Assert.Equal(owner, (await created.Content.ReadFromJsonAsync<ProfileResponse>())?.OwnerId);
        using var invalid = await client.PostAsJsonAsync("/api/v1/profiles/", new { displayName = " " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var missing = await client.PostAsJsonAsync("/api/v1/profiles/", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task MigrationsAreRepeatableAndMatchModels()
    {
        await using var profiles = new ProfilesDbContext(new DbContextOptionsBuilder<ProfilesDbContext>().UseNpgsql(postgres.ProfilesConnection).Options);
        await using var identity = new IdentityStoreContext(new DbContextOptionsBuilder<IdentityStoreContext>().UseNpgsql(postgres.IdentityConnection).Options);
        await profiles.Database.MigrateAsync();
        await identity.Database.MigrateAsync();
        Assert.False(profiles.Database.HasPendingModelChanges());
        Assert.False(identity.Database.HasPendingModelChanges());
        Assert.NotEmpty(await profiles.Database.GetAppliedMigrationsAsync());
        Assert.NotEmpty(await identity.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task FailedSaveDoesNotCommitOutbox()
    {
        var id = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ProfilesDbContext>().UseNpgsql(postgres.ProfilesConnection).Options;
        await using (var first = new ProfilesDbContext(options))
        {
            await new EfProfileStore(first).AddAsync(Profile.Create(id, Guid.NewGuid(), "First", DateTimeOffset.UtcNow), CancellationToken.None);
        }

        await using (var second = new ProfilesDbContext(options))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => new EfProfileStore(second).AddAsync(Profile.Create(id, Guid.NewGuid(), "Duplicate", DateTimeOffset.UtcNow), CancellationToken.None));
        }

        await using var verification = new ProfilesDbContext(options);
        Assert.Single(await verification.OutboxMessages.Where(m => m.AggregateId == id).ToListAsync());
    }

    [Fact]
    public async Task IdentityUsesRealPasswordHashingAndUniqueEmails()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityFoundation((_, options) => options.UseNpgsql(postgres.IdentityConnection));
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUserAccount>>();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var account = new IdentityUserAccount { Id = Guid.NewGuid(), UserName = email, Email = email };
        const string password = "OurSpace-tests-123!";
        Assert.True((await manager.CreateAsync(account, password)).Succeeded);
        Assert.NotEqual(password, account.PasswordHash);
        Assert.True(await manager.CheckPasswordAsync(account, password));
        Assert.False(await manager.CheckPasswordAsync(account, "Incorrect-password-123!"));
        var weak = new IdentityUserAccount { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N"), Email = "weak-" + email };
        Assert.False((await manager.CreateAsync(weak, "Short-1!")).Succeeded);
        var duplicate = new IdentityUserAccount { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N"), Email = email };
        Assert.False((await manager.CreateAsync(duplicate, password)).Succeeded);
    }
}
