namespace DeveloperApi.Entities;

public class DeveloperApplication
{
    public Guid Id { get; set; }

    // Application Ownership：所有查詢與異動都以此欄位作為隔離條件。
    public Guid OwnerMemberId { get; set; }

    public required string ClientId { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public required string ContactEmail { get; set; }

    public string? LogoUrl { get; set; }

    public string? HomepageUrl { get; set; }

    public ApplicationStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
