using Microsoft.EntityFrameworkCore;
using Npgsql;
using OurSpace.Bff.Infrastructure;
using OurSpace.Identity.Infrastructure;
using OurSpace.Profiles.Infrastructure;
using Xunit;

namespace OurSpace.Backend.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly List<string> databases = [];
    private string adminConnection = string.Empty;
    public string BffConnection { get; private set; } = string.Empty;
    public string ProfilesConnection { get; private set; } = string.Empty;
    public string IdentityConnection { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("OURSPACE_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Set OURSPACE_TEST_POSTGRES to a disposable local PostgreSQL instance. See docs/BACKEND_FOUNDATION.md.");
        var configuration = new NpgsqlConnectionStringBuilder(adminConnection);
        if (configuration.Database != "ourspace_test_admin" || configuration.Host is not ("127.0.0.1" or "localhost"))
        {
            throw new InvalidOperationException("Tests require a loopback PostgreSQL connection with Database=ourspace_test_admin.");
        }

        BffConnection = await CreateDatabaseAsync();
        await using var sessions = new SessionDbContext(new DbContextOptionsBuilder<SessionDbContext>().UseNpgsql(BffConnection).Options);
        await sessions.Database.MigrateAsync();
        ProfilesConnection = await CreateDatabaseAsync();
        IdentityConnection = await CreateDatabaseAsync();
        await using var profiles = new ProfilesDbContext(new DbContextOptionsBuilder<ProfilesDbContext>().UseNpgsql(ProfilesConnection).Options);
        await using var identity = new IdentityStoreContext(new DbContextOptionsBuilder<IdentityStoreContext>().UseNpgsql(IdentityConnection).Options);
        await profiles.Database.MigrateAsync();
        await identity.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        foreach (var name in databases)
        {
            await using var connection = new NpgsqlConnection(adminConnection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    internal async Task<string> CreateDatabaseAsync()
    {
        var name = "ourspace_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        databases.Add(name);
        return new NpgsqlConnectionStringBuilder(adminConnection) { Database = name }.ConnectionString;
    }
}
