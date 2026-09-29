namespace MemberApi.Entities;

// 已連結的應用程式（Connected App）：CONTEXT.md 統一領域語言，避免使用 Integration/ThirdPartyClient/ExternalApp。
public class ConnectedApp
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Identifier { get; set; }

    public string? LogoUrl { get; set; }
}
