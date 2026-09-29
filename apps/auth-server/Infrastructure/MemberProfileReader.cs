using Microsoft.EntityFrameworkCore;

namespace AuthServer.Infrastructure;

public record MemberProfile
{
    public required string Email { get; init; }

    public required string DisplayName { get; init; }

    public bool EmailVerified { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public static class MemberProfileReader
{
    // 欄位別名採 snake_case，對應 UseSnakeCaseNamingConvention 對屬性名稱的轉換。
    // ponytail: 與 member-api 共用資料庫，直接讀 members（耦合其 schema）；改為內部 API 時再抽換。
    // members 尚無 updated_at 欄位，會員資料的異動只有建立與 Email 驗證，故取兩者較晚者；
    // 日後加入資料編輯功能與 updated_at 欄位時改讀該欄位。
    public static Task<MemberProfile?> GetAsync(AuthServerDbContext dbContext, Guid memberId, CancellationToken cancellationToken) =>
        dbContext.Database
            .SqlQuery<MemberProfile>($"""
                select email, display_name,
                       email_verified_at is not null as email_verified,
                       greatest(created_at, coalesce(email_verified_at, created_at)) as updated_at
                from members where id = {memberId}
                """)
            .Cast<MemberProfile?>()
            .SingleOrDefaultAsync(cancellationToken);
}
