namespace MemberApi.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }

    public required string ToEmail { get; set; }

    public required string Subject { get; set; }

    public required string Body { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
