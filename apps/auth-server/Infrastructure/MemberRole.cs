using Microsoft.EntityFrameworkCore;

namespace AuthServer.Infrastructure;

public static class MemberRole
{
    public const string ClaimType = "role";

    // ponytail: 與 member-api 共用資料庫，直接讀 members.role（耦合其 schema）；改為內部 API 時再抽換。
    public static Task<string?> GetCurrentAsync(AuthServerDbContext dbContext, Guid memberId, CancellationToken cancellationToken) =>
        dbContext.Database
            .SqlQuery<string>($"select role as \"Value\" from members where id = {memberId}")
            .Cast<string?>()
            .SingleOrDefaultAsync(cancellationToken);
}
