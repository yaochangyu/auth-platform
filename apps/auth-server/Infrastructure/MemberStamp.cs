using Microsoft.EntityFrameworkCore;

namespace AuthServer.Infrastructure;

public static class MemberStamp
{
    public const string ClaimType = "security_stamp";

    // ponytail: 與 member-api 共用資料庫，直接讀 members.security_stamp（耦合其 schema）；改為內部 API 時再抽換。
    public static Task<string?> GetCurrentAsync(AuthServerDbContext dbContext, Guid memberId, CancellationToken cancellationToken) =>
        dbContext.Database
            .SqlQuery<string>($"select security_stamp as \"Value\" from members where id = {memberId}")
            .Cast<string?>()
            .SingleOrDefaultAsync(cancellationToken);
}
