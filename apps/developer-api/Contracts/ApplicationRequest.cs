namespace DeveloperApi.Contracts;

public record ApplicationRequest(string Name, string Description, string ContactEmail, string? LogoUrl, string? HomepageUrl);
