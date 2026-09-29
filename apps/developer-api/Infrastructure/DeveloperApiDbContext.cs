using DeveloperApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeveloperApi.Infrastructure;

public class DeveloperApiDbContext(DbContextOptions<DeveloperApiDbContext> options) : DbContext(options)
{
    // 與 member-api、auth-server 共用資料庫，migration 記錄表必須獨立。
    public const string MigrationsHistoryTable = "__ef_migrations_history_developer_api";

    public DbSet<DeveloperApplication> Applications => this.Set<DeveloperApplication>();

    public DbSet<ApiKey> ApiKeys => this.Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeveloperApplication>(builder =>
        {
            builder.ToTable("developer_applications");
            builder.HasIndex(application => application.ClientId).IsUnique();
            builder.HasIndex(application => application.OwnerMemberId);
        });

        modelBuilder.Entity<ApiKey>(builder =>
        {
            builder.ToTable("developer_api_keys");
            builder.HasIndex(key => key.KeyHash).IsUnique();
            builder.HasIndex(key => key.ApplicationId);
            builder.HasOne<DeveloperApplication>().WithMany().HasForeignKey(key => key.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
