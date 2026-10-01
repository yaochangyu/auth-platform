# 規格書：身分驗證核心深度模組化與接縫整併 (Auth Core Deep Modules & Seam Consolidation)

## Problem Statement

目前平台的身分鑑別（Auth API）與前端客戶端（Auth Web）體系在經歷多輪功能擴展後，產生了以下架構與設計痛點：
1. **虛擬接縫氾濫（Hypothetical Seams）**：`apps/member-api` 的 `AuthController` 建構函式過度膨脹（注入多達 14 個依賴），其中包含 7 個 `I*Handler` 介面（如 `IRegisterMemberHandler`, `ILoginHandler`, `IVerifyEmailHandler` 等）。依據架構準則「One adapter means a hypothetical seam. Two adapters means a real one」，這些介面皆僅有唯一實作，且純為單向轉手，通過 The Deletion Test 判定為無多態價值的虛擬接縫。
2. **登入失敗鎖定規則之知識分散（Locality 破壞）**：連續失敗次數上限（5 次）與鎖定時限（15 分鐘）等安全常數定義於 `LoginHandler`，但判斷「鎖定是否過期需重設」的邏輯又由 `MemberRepository` 內的原生 SQL 重複運算。業務知識散落於兩處，既難以修改，也無法以純記憶體單元測試覆蓋邊界時限。
3. **前端認證狀態多來源脫節（Lack of Single Source of Truth）**：`apps/member-web` 前端將登入結果暫存於模組層級的 Composable（`useAuth.ts`），而會員個人檔案卻存放於 Pinia Store（`stores/auth.ts`）。使用者登出時僅調用後端登出並重設 Composable，未連動清空 Pinia Store 的會員個人資訊，破壞客戶端狀態真實性。

## Solution

1. **拔除虛擬介面**：刪除 `apps/member-api` 中無多態價值的 7 個 `I*Handler` 介面，`AuthController` 與 DI 容器直接依賴 Concrete Handler 具體類別，精簡建構函式。
2. **建立領域深模組**：建立純邏輯的 `LoginLockoutPolicy` 領域模型，集中管理連續失敗計數累加、鎖定時間計算、鎖定中阻擋與逾期自動解鎖等全部規則；`MemberRepository` 回歸純粹的資料儲存接縫，不再內嵌業務時間計算。
3. **收斂前端狀態管理**：前端將登入態、註冊、登出與個人檔案集中由 Pinia `useAuthStore` 維護，實現登出時原子化重設（`$reset()`），消除狀態脫節問題。

## User Stories

1. As a software maintainer, I want `AuthController` to directly depend on concrete handlers instead of pass-through single-implementation interfaces, so that codebase navigation is straightforward and adheres to the deletion test.
2. As a backend developer, I want all login lockout rules encapsulated within a pure `LoginLockoutPolicy` domain model, so that business security rules are localized in one cohesive place rather than scattered across handlers and SQL queries.
3. As a QA automation engineer, I want the `LoginLockoutPolicy` to be thoroughly verified via fast, zero-dependency in-memory unit tests, so that all boundary conditions (e.g. 4 attempts vs 5 attempts, lockout expiration, count reset) can be tested deterministically without touching databases.
4. As an API platform architect, I want `MemberRepository` to act purely as a persistence adapter, so that database queries only handle data retrieval and update rather than embedding temporal business policies.
5. As an end-user on the web portal, I want my profile information, login session, and authorization states to be completely cleared whenever I click logout, so that subsequent users or shared browser sessions cannot observe stale identity data.
6. As a frontend engineer, I want a single source of truth in Pinia `useAuthStore` for all authentication and session states, so that components and router guards can reactively observe auth state without Composable variable desynchronization.
7. As a security engineer, I want account lockout thresholds and expiration behaviors to remain strictly backward-compatible with existing specifications, so that brute-force protection continues to safeguard members without regression.
8. As a system integrator, I want all existing OpenAPI v1 contracts, error response structures (RFC 7807 ProblemDetails), and DualScheme endpoints to remain completely unchanged, so that mobile clients and web SPAs require zero breaking migrations.
9. As a code reviewer, I want shallow modules and redundant interface files eliminated from the source tree, so that cognitive load is reduced and test leverage is maximized.
10. As a DevOps engineer, I want all 304 automated integration and BDD test scenarios to pass 100% cleanly following this refactoring, so that deployment confidence remains absolute.

## Implementation Decisions

- **收斂 Member API 虛擬接縫**：
  - 刪除 `apps/member-api/Handlers/` 中的 7 個單一實作介面：`IRegisterMemberHandler`, `IVerifyEmailHandler`, `ILoginHandler`, `IForgotPasswordHandler`, `IResetPasswordHandler`, `ISendSmsOtpHandler`, `IVerifyPhoneHandler`。
  - `AuthController` 建構函式改為直接注入具體類別（如 `LoginHandler`, `RegisterMemberHandler` 等）。
  - 在 `apps/member-api/Program.cs` 中直接以 Scoped 註冊 Concrete Handlers：例如 `builder.Services.AddScoped<LoginHandler>();` 等。

- **建立領域純邏輯深模組 LoginLockoutPolicy**：
  - 於 `MemberApi.Domain` 建立純邏輯、無外部依賴的 `LoginLockoutPolicy` 類別：
    - 集中封裝 `MaxFailedAttempts = 5` 與 `LockoutDuration = TimeSpan.FromMinutes(15)` 常數。
    - 提供純函式方法：例如評估當前是否處於鎖定中、計算記錄一次失敗後的全新計數與鎖定時間戳記、以及登入成功時的重設值。
  - `LoginHandler` 注入 `TimeProvider`，調用 `LoginLockoutPolicy` 進行安全判定與狀態計算。
  - 重構 `MemberRepository`：移除 SQL 中關於 `NOW() > lockout_end_at` 的重複時間運算，由 Repository 純粹執行讀出並交由領域政策判定，或依據政策計算出的結果寫入資料表。

- **前端狀態管理統一收斂至 Pinia Store**：
  - 擴充 `apps/member-web/src/stores/auth.ts`，將 `loginResult`、`login`、`register`、`logout`、`loginAndRedirect` 等登入認證行為與狀態統一由 `useAuthStore` 掌管。
  - `logout` 方法執行後端 `/api/v1/auth/logout` 呼叫，並原子化清空 `profile` 與 `loginResult`（透過 Pinia `$reset` 或明確清空）。
  - `apps/member-web/src/composables/useAuth.ts` 簡化為便利委派層（Pass-through facade）或直接逐步將視圖遷移至調用 `useAuthStore`。

## Testing Decisions

- **測試原則**：
  - 嚴格遵守「只測試對外公開行為，不刺探內部實作細節」之原則。
  - 介面即測試表面（The interface is the test surface）。
- **測試接縫（Seams）**：
  1. **最高端到端接縫（Highest Seam - WebApplicationFactory & BDD）**：
     - 現有 `tests/MemberApi.Tests` 中的 106 項 BDD 測試（涵蓋 Login、Registration、RateLimiting、SmsOtpEndpoints 等）與全方案共 304 項整合測試作為最高驗收接縫，在重構過程中與完成後必須 100% 保持綠燈。
  2. **領域政策接縫（Domain Policy Seam - LoginLockoutPolicy）**：
     - 建立 `tests/MemberApi.Tests/Domain/LoginLockoutPolicyTests.cs`，使用純單元測試覆蓋：
       - 連續失敗 1 至 4 次：未觸發鎖定，失敗計數累加。
       - 第 5 次失敗：觸發鎖定，計算出鎖定時限為當前時間 + 15 分鐘。
       - 鎖定期間內嘗試登入：直接判定為鎖定中並回傳剩餘時限。
       - 鎖定逾期後嘗試登入且密碼錯誤：重新起算失敗計數為 1。
       - 登入成功：清除鎖定狀態與失敗計數。
  3. **前端元件與狀態接縫（Web State Seam）**：
     - 執行 `npm run type-check` 確保 TypeScript 型別無錯誤。
     - 驗證登出流程在前端完全清除 Session 與 Profile。
- **既有先例（Prior Art）**：
  - 參考 `apps/member-api` 現有 `MemberApiWebApplicationFactory` 與 `tests/AuthServer.Tests` 的整合測試架構。

## Out of Scope

- 不更動對外 RESTful API 與 OpenAPI 契約（`member-api-v1.yaml` 保持 100% 相容）。
- 不更動資料庫 Schema（複用現有 `members` 資料表的 `failed_login_attempts` 與 `lockout_end_at` 欄位）。
- 不更動 `apps/auth-server` 的 OpenIddict 實體與流程。

## Further Notes

- 此規格嚴格落實 `/codebase-design` 原則，將破碎的抽象收斂為高槓桿的深模組（Deep Module），並確保符合 `/ponytail` 極簡主義（以刪除虛擬介面減少系統熵值）。
