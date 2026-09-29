using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MemberApi.Tests.Support;

public static class ConnectedAppSeeder
{
    public static async Task<MemberGrant> SeedGrantAsync(
        MemberApiWebApplicationFactory factory,
        Guid memberId,
        string appName,
        bool revoked = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();

        var app = new ConnectedApp
        {
            Id = Guid.NewGuid(),
            Name = appName,
            Identifier = $"app_{Guid.NewGuid():N}",
        };
        dbContext.ConnectedApps.Add(app);

        var now = DateTimeOffset.UtcNow;
        var grant = new MemberGrant
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            ConnectedAppId = app.Id,
            Scopes = ["read:profile", "read:email"],
            AuthorizedAt = now,
            RevokedAt = revoked ? now : null,
        };
        dbContext.MemberGrants.Add(grant);

        await dbContext.SaveChangesAsync();
        return grant;
    }
}
