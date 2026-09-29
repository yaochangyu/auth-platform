using Microsoft.EntityFrameworkCore;

namespace AuthServer.Infrastructure;

public class AuthServerDbContext(DbContextOptions<AuthServerDbContext> options) : DbContext(options)
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
