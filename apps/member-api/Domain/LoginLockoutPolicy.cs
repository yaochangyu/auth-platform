namespace MemberApi.Domain;

public record LoginLockoutState(int FailedLoginAttempts, DateTimeOffset? LockoutEndAt);

// 登入失敗鎖定規則的唯一出處：純函式，不碰資料庫與時鐘（now 由呼叫端傳入），回傳新狀態而不修改輸入。
public static class LoginLockoutPolicy
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static readonly LoginLockoutState Cleared = new(0, null);

    // 鎖定期間內一律視為鎖定，即使密碼正確也不應進行雜湊比對。
    public static bool IsLocked(LoginLockoutState state, DateTimeOffset now) => state.LockoutEndAt is { } end && end > now;

    // 鎖定已過期的失敗視為全新計數起點（從 1 開始）；累計達上限時鎖定到 now + LockoutDuration。
    public static LoginLockoutState RegisterFailure(LoginLockoutState state, DateTimeOffset now)
    {
        var expired = state.LockoutEndAt is { } end && end <= now;
        var attempts = expired ? 1 : state.FailedLoginAttempts + 1;

        if (attempts >= MaxFailedAttempts)
        {
            return new LoginLockoutState(attempts, now + LockoutDuration);
        }

        return new LoginLockoutState(attempts, expired ? null : state.LockoutEndAt);
    }
}
