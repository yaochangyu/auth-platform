using Microsoft.EntityFrameworkCore;

namespace AuthServer.Infrastructure;

// ponytail: 與 member-api 共用資料庫，直接讀 members（耦合其 schema）；改為內部 API 時換一個 Adapter 即可。
// 欄位別名採 snake_case，對應 UseSnakeCaseNamingConvention 對屬性名稱的轉換。
// members 尚無 updated_at 欄位，會員資料的異動只有建立與 Email 驗證，故取兩者較晚者；日後加入該欄位時改讀它。
public class DatabaseMemberDirectory(AuthServerDbContext dbContext) : IMemberDirectory
{
    public Task<MemberSnapshot?> GetAsync(Guid memberId, CancellationToken ct = default) =>
        dbContext.Database
            .SqlQuery<MemberSnapshot>($"""
                select security_stamp, role, email, display_name,
                       email_verified_at is not null as email_verified,
                       greatest(created_at, coalesce(email_verified_at, created_at)) as updated_at
                from members where id = {memberId}
                """)
            .Cast<MemberSnapshot?>()
            .SingleOrDefaultAsync(ct);
}
