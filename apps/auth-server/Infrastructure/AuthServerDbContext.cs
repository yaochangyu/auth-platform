using AuthShared;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Infrastructure;

public class AuthServerDbContext(DbContextOptions<AuthServerDbContext> options) : OpenIddictStoreDbContext(options)
{
    // ADR 0006：與 MemberApi 共用資料庫，migration 記錄表必須獨立，否則兩邊歷史互相覆蓋。
    public const string MigrationsHistoryTable = "__ef_migrations_history_auth_server";
}
