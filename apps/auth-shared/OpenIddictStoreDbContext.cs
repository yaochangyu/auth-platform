using Microsoft.EntityFrameworkCore;

namespace AuthShared;

// auth-server 與 developer-api 共用同一份 OpenIddict 資料表（ADR 0006 共用資料庫）。
// 資料表由 auth-server 的 migration 建立與維護，developer-api 只透過 OpenIddict 管理器讀寫，不做 migration。
public abstract class OpenIddictStoreDbContext(DbContextOptions options) : DbContext(options)
{
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
