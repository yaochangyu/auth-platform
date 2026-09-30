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

    public Task<Member?> FindByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return dbContext.Members.SingleOrDefaultAsync(member => member.PhoneNumber == phoneNumber, cancellationToken);
    }

    public Task<Member?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Members.SingleOrDefaultAsync(member => member.Id == id, cancellationToken);
    }

    public Task<VerificationToken?> FindVerificationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return dbContext.VerificationTokens
            .Include(verificationToken => verificationToken.Member)
            .SingleOrDefaultAsync(verificationToken => verificationToken.TokenHash == tokenHash, cancellationToken);
    }

    public Task<List<VerificationToken>> FindActiveVerificationTokensAsync(Guid memberId, VerificationTokenPurpose purpose, CancellationToken cancellationToken)
    {
        return dbContext.VerificationTokens
            .Where(token => token.MemberId == memberId && token.Purpose == purpose && token.UsedAt == null)
            .ToListAsync(cancellationToken);
    }

    public Task<VerificationToken?> FindLatestVerificationTokenAsync(Guid memberId, VerificationTokenPurpose purpose, CancellationToken cancellationToken)
    {
        return dbContext.VerificationTokens
            .Where(token => token.MemberId == memberId && token.Purpose == purpose)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
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
            SET failed_login_attempts = CASE WHEN lockout_end_at IS NOT NULL AND lockout_end_at <= @now THEN 1
                                             ELSE failed_login_attempts + 1 END,
                lockout_end_at = CASE
                    WHEN (CASE WHEN lockout_end_at IS NOT NULL AND lockout_end_at <= @now THEN 1
                               ELSE failed_login_attempts + 1 END) >= @maxAttempts THEN @lockoutEndAt
                    WHEN lockout_end_at <= @now THEN NULL
                    ELSE lockout_end_at END
            WHERE id = @memberId
            RETURNING failed_login_attempts, lockout_end_at;
            """;
        command.Parameters.Add(new NpgsqlParameter("memberId", memberId));
        command.Parameters.Add(new NpgsqlParameter("maxAttempts", maxAttempts));
        command.Parameters.Add(new NpgsqlParameter("lockoutEndAt", now + lockoutDuration));
        command.Parameters.Add(new NpgsqlParameter("now", now));

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

    public async Task RevokeAllTokensForMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        try
        {
            var subject = memberId.ToString();
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE openiddict_tokens SET status = 'revoked' WHERE subject = {subject} AND status = 'valid'",
                cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // 在獨立測試環境或 auth-server 資料表尚未建立時略過
        }
    }
}
