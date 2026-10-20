namespace OurSpace.Profiles.Infrastructure;

public sealed class OutboxMessage
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Producer { get; set; } = "profiles";
    public string CorrelationId { get; set; } = string.Empty;
    public string? CausationId { get; set; }
    public int SchemaVersion { get; set; }
    public Guid AggregateId { get; set; }
    public long AggregateVersion { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset? ProcessedAt { get; set; }
}
