using MemberApi.Domain;
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

    public async Task<LoginLockoutState?> UpdateLockoutAsync(
        Guid memberId,
        Func<LoginLockoutState, LoginLockoutState> transition,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var current = await dbContext.Database
            .SqlQuery<LoginLockoutState>($"select failed_login_attempts, lockout_end_at from members where id = {memberId} for update")
            .Cast<LoginLockoutState?>()
            .SingleOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            return null;
        }

        var next = transition(current);
        await dbContext.Database.ExecuteSqlAsync(
            $"update members set failed_login_attempts = {next.FailedLoginAttempts}, lockout_end_at = {next.LockoutEndAt} where id = {memberId}",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return next;
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
