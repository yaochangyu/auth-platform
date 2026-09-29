using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AuthServer.Infrastructure;

/// <summary>授權上下文，以 Data Protection 加密簽章後作為 consent_id，前端無法讀取或竄改。</summary>
public record ConsentTicket(
    Guid Id,
    Guid MemberId,
    string ClientId,
    string RedirectUri,
    string? State,
    string[] Scopes,
    string AuthorizeQuery,
    DateTimeOffset ExpiresAt);

public class ConsentTicketService(IDataProtectionProvider dataProtection, TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("AuthServer.ConsentTicket");

    // ponytail: 已使用清單存在記憶體，多實例部署或重啟後同一張票（5 分鐘內）可能被再次使用；
    // 此時仍需有效 Session 與 PKCE 才能取得 Code，風險有限，需要時改存資料庫。
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _consumed = new();

    public string Issue(Guid memberId, string clientId, string redirectUri, string? state, string[] scopes, string authorizeQuery)
    {
        var ticket = new ConsentTicket(
            Guid.NewGuid(), memberId, clientId, redirectUri, state, scopes, authorizeQuery, timeProvider.GetUtcNow() + Lifetime);
        return this._protector.Protect(JsonSerializer.Serialize(ticket));
    }

    // 偽造、竄改、過期一律回傳 null，不區分原因以免洩漏資訊。
    public ConsentTicket? Read(string consentId)
    {
        try
        {
            var ticket = JsonSerializer.Deserialize<ConsentTicket>(this._protector.Unprotect(consentId));
            return ticket is not null && ticket.ExpiresAt > timeProvider.GetUtcNow() ? ticket : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    public bool TryConsume(ConsentTicket ticket)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var expired in this._consumed.Where(pair => pair.Value <= now).Select(pair => pair.Key))
        {
            this._consumed.TryRemove(expired, out _);
        }

        return this._consumed.TryAdd(ticket.Id, ticket.ExpiresAt);
    }
}
