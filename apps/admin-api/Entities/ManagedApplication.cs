using AuthShared;

namespace AdminApi.Entities;

// developer-api 擁有的 developer_applications 表（由它的 migration 建立），admin-api 只讀取並更新審核狀態。
public class ManagedApplication
{
    public Guid Id { get; set; }

    public Guid OwnerMemberId { get; set; }

    public required string ClientId { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public required string ContactEmail { get; set; }

    public ApplicationStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

// developer-api 擁有的 developer_api_keys 表；斷路時把該專案所有金鑰標為已撤銷。
public class ManagedApiKey
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
