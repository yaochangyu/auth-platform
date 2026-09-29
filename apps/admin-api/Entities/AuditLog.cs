namespace AdminApi.Entities;

/// <summary>不可篡改的稽核紀錄：只能新增，資料庫觸發器禁止更新、刪除與清空。</summary>
public class AuditLog
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid ActorMemberId { get; set; }

    public required string ClientIp { get; set; }

    // 操作行為，例如 application.suspend。
    public required string Action { get; set; }

    public required string TargetType { get; set; }

    public required string TargetId { get; set; }

    // 變更前後的內容（JSON），查詢類操作沒有。
    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    // 補充資訊（JSON）：原因、連帶作廢的數量等。
    public string? DetailsJson { get; set; }
}
