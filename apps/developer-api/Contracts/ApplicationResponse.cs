using DeveloperApi.Entities;

namespace DeveloperApi.Contracts;

public record ApplicationResponse(
    Guid Id,
    string ClientId,
    string Name,
    string Description,
    string ContactEmail,
    string? LogoUrl,
    string? HomepageUrl,
    ApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ApplicationResponse From(DeveloperApplication application) => new(
        application.Id,
        application.ClientId,
        application.Name,
        application.Description,
        application.ContactEmail,
        application.LogoUrl,
        application.HomepageUrl,
        application.Status,
        application.CreatedAt,
        application.UpdatedAt);
}

public record ApplicationListResponse(IReadOnlyList<ApplicationResponse> Items);
