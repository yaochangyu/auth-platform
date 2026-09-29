using Microsoft.EntityFrameworkCore;

namespace AuthServer.Infrastructure;

public class AuthServerDbContext(DbContextOptions<AuthServerDbContext> options) : DbContext(options)
{
    // ADR 0006：與 MemberApi 共用資料庫，migration 記錄表必須獨立，否則兩邊歷史互相覆蓋。
    public const string MigrationsHistoryTable = "__ef_migrations_history_auth_server";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseOpenIddict();

        // OpenIddict 寫死 PascalCase 表名，覆寫以符合專案 snake_case 慣例。
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(entity.GetTableName()!.Replace("OpenIddict", "openiddict_").ToLowerInvariant());
        }
    }
}
