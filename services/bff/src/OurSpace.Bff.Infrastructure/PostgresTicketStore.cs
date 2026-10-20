using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace OurSpace.Bff.Infrastructure;

public sealed class PostgresTicketStore(IDbContextFactory<SessionDbContext> factory, IDataProtectionProvider protection) : ITicketStore
{
    public const string SessionKey = "ourspace:server_session";
    private readonly IDataProtector protector = protection.CreateProtector("OurSpace.Bff.Tickets.v1");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Guid.NewGuid().ToString("N");
        ticket.Properties.Items[SessionKey] = key;
        await using var database = await factory.CreateDbContextAsync();
        database.Sessions.Add(new SessionRecord { Id = key, Payload = Encode(ticket), ExpiresAt = Expiry(ticket) });
        await database.SaveChangesAsync();
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        await using var database = await factory.CreateDbContextAsync();
        // Renewal must not resurrect a concurrently logged-out session.
        await database.Sessions.Where(record => record.Id == key)
            .ExecuteUpdateAsync(update => update.SetProperty(record => record.Payload, Encode(ticket)).SetProperty(record => record.ExpiresAt, Expiry(ticket)));
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        await using var database = await factory.CreateDbContextAsync();
        var record = await database.Sessions.AsNoTracking().SingleOrDefaultAsync(record => record.Id == key);
        if (record is null || record.ExpiresAt <= DateTimeOffset.UtcNow) return null;
        return Decode(record.Payload);
    }

    public async Task RemoveAsync(string key)
    {
        await using var database = await factory.CreateDbContextAsync();
        await database.Sessions.Where(record => record.Id == key).ExecuteDeleteAsync();
    }

    public async Task<string?> WithLockedTicketAsync(string key,
        Func<AuthenticationTicket, CancellationToken, Task<string?>> operation, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var record = await database.Sessions.FromSqlInterpolated($"SELECT * FROM \"BffSessions\" WHERE \"Id\" = {key} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (record is null || record.ExpiresAt <= DateTimeOffset.UtcNow) return null;
        var ticket = Decode(record.Payload);
        if (ticket is null) return null;
        var token = await operation(ticket, cancellationToken);
        if (token is null) database.Sessions.Remove(record);
        else record.Payload = Encode(ticket);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return token;
    }

    private byte[] Encode(AuthenticationTicket ticket) => protector.Protect(TicketSerializer.Default.Serialize(ticket));
    private AuthenticationTicket? Decode(byte[] payload)
    {
        try { return TicketSerializer.Default.Deserialize(protector.Unprotect(payload)); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }
    private static DateTimeOffset Expiry(AuthenticationTicket ticket) => ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow;
}
