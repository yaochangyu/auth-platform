using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MemberApi.Tests.Support;

public static class MemberSeeder
{
    // ponytail: find-or-create 並在重用既有列時把可變狀態正規化回呼叫端指定的值，
    // 因為 Scenario Outline 的每個 Example、甚至不同 Feature 都可能共用同一組固定 Email，
    // 若前一個情境改過密碼/鎖定狀態，後面重用同一筆會員的情境不該繼承那些殘留變更。
    public static async Task<Member> SeedMemberAsync(
        MemberApiWebApplicationFactory factory,
        string email,
        MemberStatus status,
        string password = "Original@Passw0rd1")
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Member>>();

        var existing = await dbContext.Members.SingleOrDefaultAsync(m => m.Email == email);
        if (existing is not null)
        {
            existing.Status = status;
            existing.PasswordHash = hasher.HashPassword(existing, password);
            existing.SecurityStamp = Guid.NewGuid().ToString("N");
            existing.FailedLoginAttempts = 0;
            existing.LockoutEndAt = null;
            await dbContext.SaveChangesAsync();
            return existing;
        }

        var member = new Member
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "測試會員",
            PasswordHash = string.Empty,
            Status = status,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        member.PasswordHash = hasher.HashPassword(member, password);
        dbContext.Members.Add(member);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // xUnit 可能並行執行不同 Feature 的測試類別，兩個情境幾乎同時對同一個尚不存在的
            // Email 呼叫本方法時會一起走到 Insert 分支而撞唯一索引；退回改用已成功寫入的那筆。
            dbContext.Entry(member).State = EntityState.Detached;
            return await dbContext.Members.SingleAsync(m => m.Email == email);
        }

        return member;
    }
}
