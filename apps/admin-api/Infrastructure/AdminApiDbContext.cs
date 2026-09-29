using AdminApi.Entities;
using AuthShared;
using Microsoft.EntityFrameworkCore;

namespace AdminApi.Infrastructure;

// 單一 DbContext 涵蓋斷路器要動到的所有表（developer 專案與 API Key、OpenIddict、稽核紀錄），
// 才能在同一個資料庫交易內完成「停用、撤銷、寫稽核」，任何一步失敗就全部回復。
// 只有 audit_logs 由 admin-api 的 migration 建立；其餘表屬於 developer-api 與 auth-server，從 migration 排除。
public class AdminApiDbContext : OpenIddictStoreDbContext
{
    public const string MigrationsHistoryTable = "__ef_migrations_history_admin_api";

    public AdminApiDbContext(DbContextOptions<AdminApiDbContext> options)
        : base(options)
    {
    }

    protected AdminApiDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<ManagedApplication> Applications => this.Set<ManagedApplication>();

    public DbSet<ManagedApiKey> ApiKeys => this.Set<ManagedApiKey>();

    public DbSet<AuditLog> AuditLogs => this.Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ManagedApplication>().ToTable("developer_applications");
        modelBuilder.Entity<ManagedApiKey>().ToTable("developer_api_keys");
        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.ToTable("audit_logs");
            builder.Property(log => log.BeforeJson).HasColumnType("jsonb");
            builder.Property(log => log.AfterJson).HasColumnType("jsonb");
            builder.Property(log => log.DetailsJson).HasColumnType("jsonb");
            builder.HasIndex(log => log.OccurredAt);
            builder.HasIndex(log => log.TargetId);
            builder.HasIndex(log => log.ActorMemberId);
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(entity => entity.ClrType != typeof(AuditLog)))
        {
            entity.SetIsTableExcludedFromMigrations(true);
        }
    }
}
