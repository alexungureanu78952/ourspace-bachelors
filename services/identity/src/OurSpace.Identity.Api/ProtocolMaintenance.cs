using OpenIddict.Abstractions;

namespace OurSpace.Identity.Api;

public sealed partial class ProtocolMaintenance(IServiceScopeFactory scopes, ILogger<ProtocolMaintenance> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Protocol-record cleanup failed; it will retry on the next interval.")]
    private static partial void CleanupFailed(ILogger logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var threshold = DateTimeOffset.UtcNow.AddDays(-1);
                    await scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>().PruneAsync(threshold, stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>().PruneAsync(threshold, stoppingToken);
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
