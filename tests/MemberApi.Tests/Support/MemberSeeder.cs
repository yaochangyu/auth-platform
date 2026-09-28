using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MemberApi.Tests.Support;

public static class MemberSeeder
{
    // ponytail: find-or-create，因為 Scenario Outline 的每個 Example 都是獨立情境但共用同一個容器化資料庫、
    // 未做情境級重置；同一組 Given 文字（含固定 Email）會被執行多次，直接 Insert 會撞 Email 唯一索引。
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
            return existing;
        }

        var member = new Member
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "測試會員",
            PasswordHash = string.Empty,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        member.PasswordHash = hasher.HashPassword(member, password);
        dbContext.Members.Add(member);
        await dbContext.SaveChangesAsync();

        return member;
    }
}
