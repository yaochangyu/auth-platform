# 授權伺服器會員目錄深度模組架構重構 (IMemberDirectory Deep Module Refactoring) 規格

- **GitHub Issue**: [#38](https://github.com/yaochangyu/auth-platform/issues/38)
- **狀態**: `ready-for-agent`

## Problem Statement

目前在授權伺服器（`apps/auth-server`）中，權杖換發（Token Exchange）與使用者資訊（UserInfo）查詢散落於多個獨立的靜態讀取類別中。這些類別各自撰寫原生 SQL 查詢共用資料庫的 `members` 資料表，資料表名稱、欄位名稱與別名散落在各處，違反 Locality（局部性）原則。

特別是在 Refresh Token 換發授權流程中，控制器必須先後發起兩次獨立的資料庫往返（一次查詢 Security Stamp 驗證權杖有效性、一次查詢會員角色 Role 指派權限聲明）。

此外，既有程式碼雖標註未來可能將資料庫直連抽換為內部 API，但因目前抽換點多達三個且缺乏統一介面接縫（Seam），若未來進行資料來源切換，將導致散彈式修改（Shotgun Surgery）。且靜態方法高度耦合資料庫連線，使測試受限於實體資料庫，無法透過輕量 Fake 隔離驗證特定權限或安全性戳記變更情境。

## Solution

依據深度模組設計原則（Deep Module Design - Small Interface + Deep Implementation），在授權伺服器中建立統一的會員目錄抽象接縫 `IMemberDirectory`：

1. **極小化介面（Small Interface）**：僅暴露單一非同步查詢方法 `GetAsync`，接收會員識別碼並回傳具備完整欄位快照的不可變記錄 `MemberSnapshot`。
2. **高效能實作（Deep Implementation）**：將原本散落的三份原生 SQL 查詢整合成單一最佳化 SQL，在一次資料庫往返（Single Round-trip）中同時取得 Security Stamp、Role、Email、DisplayName、EmailVerified 與 UpdatedAt，將換票時的資料庫往返次數減半。
3. **唯一的未來抽換接縫（Single Real Seam）**：為共用資料庫提供明確的 Adapter 實作，未來若抽換為內部微服務或 HTTP/gRPC API，全授權伺服器僅需抽換此單一接縫的實作，呼叫端與控制器完全不受影響。
4. **提升可測試性（Accept Dependencies & Pure Results）**：透過標準相依性注入（DI）管理，單元與情境測試可注入 Fake 實作，快速驗證會員遭停用、管理員降級換票、安全戳記失效等邊界情況，無須強制依賴實體資料庫啟動。

## User Stories

1. As an OAuth client application exchanging a refresh token, I want the authorization server to evaluate my request with a single database round-trip, so that token issuance latency is significantly reduced.
2. As an OAuth client application requesting user information (/connect/userinfo), I want consistent and up-to-date member claims returned from the directory, so that the user profile accurately reflects their current state.
3. As an authorization server maintainer, I want all SQL queries targeting the members table to be encapsulated within a single deep module, so that schema changes or column renames only require edits in one local place.
4. As an authorization server maintainer, I want a unified `IMemberDirectory` seam, so that future transitions from shared database access to an internal microservice API require altering only one adapter without touching business controllers.
5. As a developer writing tests for authorization flows, I want to substitute `IMemberDirectory` with a fast in-memory fake, so that I can simulate security stamp revocations, role changes, and deactivated users deterministically without spinning up a full database container.
6. As a security architect, I want the authorization server to consistently reject revoked refresh tokens by verifying the latest security stamp from the snapshot, so that logged-out or compromised sessions are strictly invalidated across all devices.
7. As a security architect, I want member role transitions (such as role downgrade or promotion) to take effect immediately upon next token refresh, so that privileges are strictly aligned with the current identity directory.
8. As a DevOps engineer, I want database connection lifecycles within `IMemberDirectory` to be managed via standard dependency injection, so that connection pooling and cancellation tokens are honored properly across concurrent authorization requests.
9. As an API platform engineer, I want the refactored directory module to maintain strict backward compatibility with existing OpenIddict flows, so that no external client integration is broken.
10. As a code reviewer, I want shallow, redundant static helper classes removed in favor of a cohesive deep module, so that codebase navigation is straightforward and adheres to the deletion test.

## Implementation Decisions

- **深度模組定義與介面契約**：
  - 定義介面 `IMemberDirectory`，維持單一公開方法：
    ```csharp
    public interface IMemberDirectory
    {
        Task<MemberSnapshot?> GetAsync(Guid memberId, CancellationToken ct = default);
    }

    public record MemberSnapshot(
        string SecurityStamp,
        string Role,
        string Email,
        string DisplayName,
        bool EmailVerified,
        DateTimeOffset UpdatedAt);
    ```
  - `MemberSnapshot` 完整囊括身分鑑別（Security Stamp）、授權核發（Role）與使用者個人檔案（Email、DisplayName、EmailVerified、UpdatedAt），杜絕多次碎片化查詢。
- **資料庫適配器實作（Database Adapter）**：
  - 建立 `DatabaseMemberDirectory` 實作 `IMemberDirectory`。
  - 將既有分散在 `MemberStamp`、`MemberRole` 與 `MemberProfileReader` 中的三個原生 SQL 查詢整合成單一查詢。
  - 實作接收外部注入的 `AuthDbContext` 或連線工廠，遵守「接受相依性而非自行建立（Accept dependencies）」原則。
- **呼叫端重構與收斂**：
  - 重構 `apps/auth-server` 中的 `TokenController`：在權杖換發邏輯中注入 `IMemberDirectory`，以單次調用取得 Security Stamp 與 Role，消除二次資料庫往返。
  - 重構 `UserInfoController`：改為調用 `IMemberDirectory.GetAsync`，取代既有的靜態讀取輔助類別。
  - 廢棄並移除冗餘的靜態讀取類別（`MemberStamp.cs`、`MemberRole.cs`、`MemberProfileReader.cs`），通過 Deletion Test。
- **服務註冊**：
  - 在 `apps/auth-server/Program.cs` 中將 `DatabaseMemberDirectory` 註冊為 Scoped 服務：`builder.Services.AddScoped<IMemberDirectory, DatabaseMemberDirectory>();`。

## Testing Decisions

- **測試原則**：
  - 嚴格遵守「只測試對外公開行為，不刺探內部實作細節」之原則。
  - 介面即測試表面（The interface is the test surface）。
- **測試接縫（Seams）**：
  1. **最高端到端接縫（Highest Seam - WebApplicationFactory & BDD）**：
     - 現有 `tests/AuthServer.Tests` 中的 86 項 BDD 整合測試（涵蓋 TokenRevocation、PublicClientPkce、UserInfo 等流程）必須在重構後 100% 保持綠燈。
  2. **模組接縫測試（Module Seam - FakeMemberDirectory）**：
     - 針對特定權限變更或邊界情況（例如查詢不存在之會員、會員安全戳記變更導致換票拒絕），建立基於記憶體的 `FakeMemberDirectory` 進行高槓桿、零資料庫相依的控制器邏輯驗證。
- **既有先例（Prior Art）**：
  - 參考 `apps/auth-server` 現有 `TokenRevocationTests` 與 `PublicClientPkceTests` 中使用 `WebApplicationFactory` 的架構。

## Out of Scope

- **不更動資料庫 Schema**：本次重構完全複用現有的 `members` 資料表結構，無須新增或修改任何 EF Core Migration。
- **不實作遠端 HTTP/RPC 微服務客戶端**：本次僅建立抽象 Seam 並提供 `DatabaseMemberDirectory`，未來若有獨立微服務需求時再行實作第二個 Adapter。
- **不更動 `apps/member-api` 的登入與會員中心邏輯**：本次聚焦於授權伺服器（`apps/auth-server`）的會員目錄收斂。

## Further Notes

- 此規格嚴格遵守 `/codebase-design` 原則，將複雜度收斂至深模組內部，並確保符合 `/ponytail` 極簡主義（刪除 3 個靜態分散模組，代之以單一凝聚介面，刪除大於新增）。
