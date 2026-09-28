using MemberApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MemberApi.Infrastructure.Persistence;

public class MemberApiDbContext(DbContextOptions<MemberApiDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => this.Set<Member>();

    public DbSet<VerificationToken> VerificationTokens => this.Set<VerificationToken>();

    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();

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
    }
}
