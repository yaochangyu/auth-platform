using Microsoft.EntityFrameworkCore;

namespace AuthServer.Infrastructure;

// ponytail: 與 member-api 共用資料庫，直接讀 members（耦合其 schema）；改為內部 API 時換一個 Adapter 即可。
// 欄位別名採 snake_case，對應 UseSnakeCaseNamingConvention 對屬性名稱的轉換。
// 會員資料的異動有三種：建立、Email 驗證（只寫 email_verified_at）、編輯個人檔案（寫 updated_at），故取三者最晚者。
public class DatabaseMemberDirectory(AuthServerDbContext dbContext) : IMemberDirectory
{
    public async Task<MemberSnapshot?> GetAsync(Guid memberId, CancellationToken ct = default) =>
        await dbContext.Database
            .SqlQuery<MemberSnapshot>($"""
                select security_stamp, role, email, display_name,
                       email_verified_at is not null as email_verified,
                       greatest(created_at, coalesce(email_verified_at, created_at), coalesce(updated_at, created_at)) as updated_at
                from members where id = {memberId}
                """)
            .Cast<MemberSnapshot?>()
            .SingleOrDefaultAsync(ct);
}
