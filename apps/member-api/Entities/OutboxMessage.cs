namespace MemberApi.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }

    public required string ToEmail { get; set; }

    public required string Subject { get; set; }

    public required string Body { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; } = 5;

    public string? ErrorMessage { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }
}
