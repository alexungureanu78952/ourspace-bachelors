using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OurSpace.Profiles.Infrastructure;
using Xunit;

namespace OurSpace.Backend.IntegrationTests;

public sealed class ProfileUniquenessTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ConcurrentCreationCommitsExactlyOneProfileAndEvent()
    {
        var owner = Guid.NewGuid();
        await using var factory = new ProfileApiFactory(postgres.ProfilesConnection);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", owner.ToString());
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(index =>
            client.PostAsJsonAsync("/api/v1/profiles/", new { displayName = "Profile " + index })));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            await using var database = Context(postgres.ProfilesConnection);
            var profile = Assert.Single(await database.Profiles.Where(profile => profile.OwnerId == owner).ToListAsync());
            Assert.False(profile.IsArchived);
            Assert.Single(await database.OutboxMessages.Where(message => message.AggregateId == profile.Id).ToListAsync());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task MigrationPreservesDuplicatesAndHidesArchivedProfiles()
    {
        var connection = await postgres.CreateDatabaseAsync();
        var owner = Guid.NewGuid();
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var third = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using (var database = Context(connection))
        {
            await database.GetService<IMigrator>().MigrateAsync("20261020065745_InitialProfiles");
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Profiles" ("Id", "OwnerId", "DisplayName", "CreatedAt")
                VALUES ({third}, {owner}, 'Later', {created.AddDays(1)}),
                       ({second}, {owner}, 'Tied', {created}),
                       ({first}, {owner}, 'Oldest', {created});
                """);
            await database.Database.MigrateAsync();
        }

        await using var verification = Context(connection);
        var profiles = await verification.Profiles.Where(profile => profile.OwnerId == owner).ToListAsync();
        Assert.Equal(3, profiles.Count);
        Assert.Equal(first, Assert.Single(profiles, profile => !profile.IsArchived).Id);
        Assert.Equal(2, profiles.Count(profile => profile.IsArchived));
        var store = new EfProfileStore(verification);
        Assert.NotNull(await store.FindAsync(first, CancellationToken.None));
        Assert.Null(await store.FindAsync(second, CancellationToken.None));
        Assert.Null(await store.FindAsync(third, CancellationToken.None));
        await verification.Database.MigrateAsync();
        Assert.Equal(3, await verification.Profiles.CountAsync());
    }

    private static ProfilesDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<ProfilesDbContext>().UseNpgsql(connection).Options);
}
