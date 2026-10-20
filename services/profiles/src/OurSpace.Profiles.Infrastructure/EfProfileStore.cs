using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OurSpace.Profiles.Application;
using OurSpace.Profiles.Domain;

namespace OurSpace.Profiles.Infrastructure;

public sealed class EfProfileStore(ProfilesDbContext database) : IProfileStore
{
    public async Task AddAsync(Profile profile, CancellationToken cancellationToken)
    {
        database.Profiles.Add(profile);
        database.OutboxMessages.Add(new OutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = "profiles.profile-created.v1",
            SchemaVersion = 1,
            CorrelationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            AggregateId = profile.Id,
            AggregateVersion = 1,
            OccurredAt = profile.CreatedAt,
            Payload = JsonSerializer.Serialize(new { profileId = profile.Id, ownerId = profile.OwnerId }),
        });
        // EF commits the profile and event in the same local transaction.
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Profiles_OwnerId" })
        {
            throw new ProfileAlreadyExistsException(exception);
        }
    }

    public Task<Profile?> FindAsync(Guid profileId, CancellationToken cancellationToken) =>
        database.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == profileId && !p.IsArchived, cancellationToken);
}
