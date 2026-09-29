using MemberApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MemberApi.Infrastructure.Persistence;

public class MemberApiDbContext(DbContextOptions<MemberApiDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => this.Set<Member>();

    public DbSet<VerificationToken> VerificationTokens => this.Set<VerificationToken>();

    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();

    public DbSet<ConnectedApp> ConnectedApps => this.Set<ConnectedApp>();

    public DbSet<MemberGrant> MemberGrants => this.Set<MemberGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Member>(builder =>
        {
            builder.HasIndex(member => member.Email).IsUnique();
        });

        modelBuilder.Entity<VerificationToken>(builder =>
        {
            builder.HasIndex(token => token.TokenHash).IsUnique();
            builder.HasOne(token => token.Member)
                .WithMany()
                .HasForeignKey(token => token.MemberId);
        });

        modelBuilder.Entity<ConnectedApp>(builder =>
        {
            builder.HasIndex(app => app.Identifier).IsUnique();
        });

        modelBuilder.Entity<MemberGrant>(builder =>
        {
            builder.HasOne(grant => grant.Member)
                .WithMany()
                .HasForeignKey(grant => grant.MemberId);
            builder.HasOne(grant => grant.ConnectedApp)
                .WithMany()
                .HasForeignKey(grant => grant.ConnectedAppId);
        });
    }
}
