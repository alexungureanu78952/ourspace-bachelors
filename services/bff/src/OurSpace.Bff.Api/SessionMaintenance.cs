using Microsoft.EntityFrameworkCore;
using OurSpace.Bff.Infrastructure;

namespace OurSpace.Bff.Api;

public sealed partial class SessionMaintenance(IDbContextFactory<SessionDbContext> factory, ILogger<SessionMaintenance> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Expired-session cleanup failed; it will retry on the next interval.")]
    private static partial void CleanupFailed(ILogger logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            do
            {
                try
                {
                    await using var database = await factory.CreateDbContextAsync(stoppingToken);
                    await database.Sessions.Where(session => session.ExpiresAt <= DateTimeOffset.UtcNow).ExecuteDeleteAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    CleanupFailed(logger);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
