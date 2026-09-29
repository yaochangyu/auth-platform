namespace MemberApi.Tests.Support;

/// <summary>
/// 涵蓋 ProblemDetails / ValidationProblemDetails / LockoutProblemDetails 所有欄位的單一測試端反序列化模型，
/// 避免針對不同錯誤回應形態各自定義型別、或需要把同一個回應內容 body 讀取多次。
/// </summary>
public record ProblemDetailsPayload(
    string? Type,
    string? Title,
    int? Status,
    Dictionary<string, string[]>? Errors,
    int? FailedLoginAttempts,
    DateTimeOffset? LockoutEndAt);
