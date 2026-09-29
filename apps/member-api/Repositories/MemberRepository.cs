using System.Data;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MemberApi.Repositories;

public class MemberRepository(MemberApiDbContext dbContext) : IMemberRepository
{
    public Task<Member?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return dbContext.Members.SingleOrDefaultAsync(member => member.Email == email, cancellationToken);
    }

    public Task<VerificationToken?> FindVerificationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return dbContext.VerificationTokens
            .Include(verificationToken => verificationToken.Member)
            .SingleOrDefaultAsync(verificationToken => verificationToken.TokenHash == tokenHash, cancellationToken);
    }

    public Task<List<VerificationToken>> FindActiveVerificationTokensAsync(Guid memberId, CancellationToken cancellationToken)
    {
        return dbContext.VerificationTokens
            .Where(token => token.MemberId == memberId && token.UsedAt == null)
            .ToListAsync(cancellationToken);
    }

    public void AddMember(Member member)
    {
        dbContext.Members.Add(member);
    }

    public void AddVerificationToken(VerificationToken verificationToken)
    {
        dbContext.VerificationTokens.Add(verificationToken);
    }

    public void AddOutboxMessage(OutboxMessage outboxMessage)
    {
        dbContext.OutboxMessages.Add(outboxMessage);
    }

    public async Task<(int FailedLoginAttempts, DateTimeOffset? LockoutEndAt)?> RegisterFailedLoginAsync(
        Guid memberId,
        DateTimeOffset now,
        int maxAttempts,
        TimeSpan lockoutDuration,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE members
            SET failed_login_attempts = failed_login_attempts + 1,
                lockout_end_at = CASE
                    WHEN failed_login_attempts + 1 >= @maxAttempts THEN @lockoutEndAt
                    ELSE lockout_end_at
                END
            WHERE id = @memberId
            RETURNING failed_login_attempts, lockout_end_at;
            """;
        command.Parameters.Add(new NpgsqlParameter("memberId", memberId));
        command.Parameters.Add(new NpgsqlParameter("maxAttempts", maxAttempts));
        command.Parameters.Add(new NpgsqlParameter("lockoutEndAt", now + lockoutDuration));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var failedLoginAttempts = reader.GetInt32(0);
        var lockoutEndAt = reader.IsDBNull(1) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(1);
        return (failedLoginAttempts, lockoutEndAt);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
