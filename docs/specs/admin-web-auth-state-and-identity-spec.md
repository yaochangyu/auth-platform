# 規格書：管理後台認證狀態深模組化、身分顯示與全鏈路登出 (Admin Web Auth State, Identity & Single Sign-Out)

## Problem Statement

目前平台的管理後台（[`apps/admin-web`](file:///home/yao/projects/auth-platform/apps/admin-web)）承載全平台最高權限之專案審核與緊急斷路操作，但在前端狀態設計與身分管理上存在以下架構痛點：

1. **認證狀態分裂與淺模組（State Fragmentation & Shallow Module）**：
   - [`stores/auth.ts`](file:///home/yao/projects/auth-platform/apps/admin-web/src/stores/auth.ts) 僅為極薄的狀態容器（只記錄 `accessToken` 與 `expiresAt`），無實質業務深度。
   - 與登入生命週期息息相關的邏輯（PKCE 授權碼流 `startLogin`、換票 `completeLogin`、防 401 迴圈守衛 `justLoggedIn`、錯誤狀態 `error`）全部分散在 [`useOAuth.ts`](file:///home/yao/projects/auth-platform/apps/admin-web/src/composables/useOAuth.ts) 與 [`useApi.ts`](file:///home/yao/projects/auth-platform/apps/admin-web/src/composables/useApi.ts) 中。
   - `useOAuth()` 每次調用皆重新建立局部的 `const error = ref<string | null>(null)`，導致 [`OAuthCallbackView.vue`](file:///home/yao/projects/auth-platform/apps/admin-web/src/views/OAuthCallbackView.vue) 與全域元件的錯誤狀態無法共用與連動，破壞單一真實來源（Single Source of Truth）。
2. **管理員身分識別缺失**：
   - 頂部導覽列 [`AppHeader.vue`](file:///home/yao/projects/auth-platform/apps/admin-web/src/components/AppHeader.vue) 僅有「管理後台」、「應用程式」、「稽核紀錄」三個文字連結，完全未呈現當前登入之管理員帳號（Email / DisplayName）。管理員在多重視窗或共享工作站操作時，無法直觀確認目前操作的身分主體。
3. **缺乏主動登出與會話註銷機制（Single Sign-Out 缺口）**：
   - 管理後台介面目前完全無「登出」按鈕。
   - 因為 `admin-web` 依賴瀏覽器 Session Cookie（`.AspNetCore.Cookies`）走 auth-server 的無感換票，即便管理員關閉分頁或手動清空前端快取，後端 Session 依然有效；共用裝置上的下一位使用者進入網址後將自動被無感換票登入，對具備停用專案與作廢金鑰特權之管理後台構成嚴重安全漏洞。
4. **OAuth 授權範疇（Scope）缺漏**：
   - [`auth-server/Infrastructure/ClientSeeder.cs`](file:///home/yao/projects/auth-platform/apps/auth-server/Infrastructure/ClientSeeder.cs) 在註冊 `admin-web` 時，僅設定了 `openid profile admin_api`，未授與 `Scopes.Email`，導致無法自既有 OIDC 標準端點取得管理員電子郵件。

---

## Solution

1. **認證狀態收斂至 Pinia Store（深模組化）**：
   - 擴充 [`apps/admin-web/src/stores/auth.ts`](file:///home/yao/projects/auth-platform/apps/admin-web/src/stores/auth.ts)，將 `accessToken`、`profile`、`error`、`isLoading` 與核心動作（`startLogin`、`completeLogin`、`fetchProfile`、`logout`、`justLoggedIn`）全數內聚於 `useAuthStore`，維持唯一的單一真實來源。
   - 將 [`apps/admin-web/src/composables/useOAuth.ts`](file:///home/yao/projects/auth-platform/apps/admin-web/src/composables/useOAuth.ts) 轉型為純委派 Facade，對接 `useAuthStore`，確保既有呼叫端相容性，杜絕散彈式修改（Shotgun Surgery）。
2. **利用現成 OIDC UserInfo 端點補齊管理員資訊**：
   - 在 [`ClientSeeder.cs`](file:///home/yao/projects/auth-platform/apps/auth-server/Infrastructure/ClientSeeder.cs) 為 `admin-web` 註冊追加 `Scopes.Email`。
   - 前端換票成功後，由 `useAuthStore` 自動調用現成的 [`auth-server/connect/userinfo`](file:///home/yao/projects/auth-platform/apps/auth-server/Controllers/UserInfoController.cs) 端點，取得管理員 `email` 與 `nickname` 並儲存於 Store。
3. **實作雙軌原子化登出（Single Sign-Out）與身分導覽**：
   - 在 [`AppHeader.vue`](file:///home/yao/projects/auth-platform/apps/admin-web/src/components/AppHeader.vue) 右上方呈現當前管理員 Email / DisplayName，並新增「登出」操作按鈕。
   - 登出時由 Store 執行原子化 `$reset()` 清空記憶體中 Token 與 Profile，並發送請求至 [`member-api/api/v1/auth/logout`](file:///home/yao/projects/auth-platform/apps/member-api/Controllers/AuthController.cs) 註銷網域共用的 `.AspNetCore.Cookies`，最後安全導回登入流程，徹底清除會話殘留。

---

## User Stories

1. As a platform administrator, I want to clearly see my logged-in email and display name in the top navigation header of the Admin Web portal, so that I can instantly verify my active identity before performing sensitive administrative actions.
2. As a platform administrator on a shared workstation, I want an explicit logout button in the header, so that I can safely end my session when finished without leaving administrative privileges exposed.
3. As a platform administrator logging out, I want both my frontend memory token and the underlying SSO browser session cookies to be revoked simultaneously, so that a subsequent user cannot automatically regain admin access by reopening the browser.
4. As a frontend engineer, I want all authentication, token acquisition, and profile state managed centrally within Pinia `useAuthStore`, so that there is a single source of truth across all components and router hooks.
5. As a frontend engineer, I want `useOAuth.ts` to remain available as a clean Facade that delegates to `useAuthStore`, so that existing view components require zero breaking refactoring.
6. As a security architect, I want the Admin Web portal to leverage the existing OpenID Connect `/connect/userinfo` endpoint rather than introducing custom unstandardized API endpoints, so that identity retrieval follows standard OIDC protocols.
7. As an identity provider engineer, I want `admin-web` seeded with `email` scope alongside `openid`, `profile`, and `admin_api`, so that the UserInfo endpoint can legitimately supply verified email addresses under principle of least privilege.
8. As a QA automation engineer, I want the logout process to guarantee state resetting via `try...finally` even if network requests fail or the backend is temporarily unreachable, so that the client-side state is never left in an inconsistent zombie state.
9. As a software maintainer, I want `AdminApi` backend services to remain completely untouched without unnecessary passthrough wrappers, adhering strictly to the ponytail minimal principle and the deletion test.
10. As a DevOps engineer, I want the entire solution's 314 existing backend tests and frontend type-checking (`vue-tsc -b`) to pass with zero errors and zero warnings, so that architectural deepening introduces no behavioral regressions.

---

## Implementation Decisions

- **授權伺服器 Client 種子配置調整（Auth Server Seam）**：
  - 在 `apps/auth-server/Infrastructure/ClientSeeder.cs` 中，將 `admin-web` 的授權範疇清單更新為包含 `Scopes.Email`：
    `[Scopes.OpenId, Scopes.Profile, Scopes.Email, AuthScopes.AdminApi]`。
  - 在 `apps/admin-web/src/stores/auth.ts`（與 `useOAuth.ts` Facade）中的授權請求參數將 Scope 設定為：
    `openid profile email admin_api`。

- **Pinia Store 深模組化架構（Admin Web Store Seam）**：
  - 擴充 `apps/admin-web/src/stores/auth.ts`：
    - **State**:
      - `accessToken: string | null`
      - `expiresAt: number`
      - `profile: { sub: string; email?: string; nickname?: string } | null`
      - `isLoading: boolean`
      - `error: string | null`
    - **Getters**:
      - `isAuthenticated`: 依據 `accessToken !== null && Date.now() < expiresAt - 10_000` 判定。
      - `displayIdentity`: 優先回傳 `profile.email` 或 `profile.nickname`，若無則回傳 `profile.sub`。
    - **Actions**:
      - `startLogin(returnPath: string)`: 產生 PKCE 隨機參數，寫入 `sessionStorage`，並導向至 `auth-server/connect/authorize`。
      - `completeLogin(query: LocationQuery): Promise<string | null>`: 驗證 `state` 與 `verifier`，呼叫 `/connect/token` 換票；換票成功後寫入 `accessToken` 與 `expiresAt`，接著非同步呼叫 `fetchProfile()` 載入管理員身分，並記錄 `lastLoginAt`。
      - `fetchProfile(): Promise<void>`: 攜帶 Bearer Token 呼叫 `auth-server/connect/userinfo`，解析並保存會員資訊。
      - `logout(): Promise<void>`:
        - 採用 `try ... finally` 架構。
        - 於 `try` 區塊中以 `credentials: 'include'` 向 `member-api` 之 `POST /api/v1/auth/logout` 發送會話註銷請求（忽略錯誤回傳）。
        - 於 `finally` 區塊中無條件調用 `this.$reset()` 清空所有狀態，並導向至 `/oauth/callback` 或登出頁面。
      - `justLoggedIn(): boolean`: 封裝 30 秒內的重複登入守衛邏輯，杜絕 401 循環跳轉。

- **Facade 委派相容層（Composable Seam）**：
  - 保留 `apps/admin-web/src/composables/useOAuth.ts`，其內部實作改為直接調用 `useAuthStore`：
    - `const store = useAuthStore()`
    - 匯出 `error: computed(() => store.error)`，並將 `startLogin`、`completeLogin` 與 `justLoggedIn` 直接對齊 Store Action。
    - 消除每次調用 `useOAuth()` 時獨立建立 `ref(null)` 之多實例異味。

- **管理導覽列 UI 增強（Admin Web UI Component）**：
  - 修改 `apps/admin-web/src/components/AppHeader.vue`：
    - 引入 `useAuthStore`。
    - 右側新增資訊區塊：若已驗證（`isAuthenticated`），顯示管理員身分標籤（例如 `store.displayIdentity`）以及「登出」按鈕。
    - 點擊「登出」觸發 `store.logout()`，並以 `AlertDialog` 或直接觸發確認對齊管理體驗。

---

## Testing Decisions

- **測試原則**：
  - 專注於外部觀察行為（OIDC UserInfo 換取、Profile 呈現、登出時 Cookie 與 Token 之清空），不測試內部暫存實作細節。
- **測試接縫（Seams）**：
  1. **前端構建與型別檢核接縫（Frontend Build & Typecheck Seam）**：
     - 執行 `npm --prefix apps/admin-web run build`（包含 `vue-tsc -b` 與 `vite build`），確保型別宣告、Pinia Store 介面與 Vue 元件契約 100% 通過，零警告零錯誤。
  2. **後端全方案回歸驗收接縫（Backend Solution Regression Seam）**：
     - 執行 `dotnet test AuthPlatform.slnx`，驗證全方案 314 項現有測試（包含 `AdminApi.Tests` 35 項、`AuthServer.Tests` 90 項、`MemberApi.Tests` 116 項、`DeveloperApi.Tests` 73 項）維持 100% 綠燈。
  3. **OIDC 端點與 ClientSeeder 整合接縫**：
     - 確保 `auth-server` 在種子同步後，能正確簽發帶有 `email` scope 之權杖，且 `/connect/userinfo` 能成功被 Admin Web 存取。

---

## Out of Scope

- **不修改 `AdminApi` 後端**：`AdminApi` 控制器與 `ApplicationStatusService` 維持現狀，不增設多餘的 Controller 端點或中介層。
- **不改動外部第三方 OAuth 授權邏輯**：本規格僅限於第一方管理客戶端（`admin-web`）之登入態、身分讀取與會話清理。
- **不引入外部 UI 套件**：使用專案既有的 Tailwind 與 Radix/shadcn-vue 元件完成按鈕與標籤呈現。

---

## Further Notes

- 本規格嚴格遵循 [ADR 0011](file:///home/yao/projects/auth-platform/docs/adr/0011-auth-deep-modules-and-seam-consolidation.md) 之架構指導原則，將管理前端認證與會話收納為高內聚之深模組（Deep Module），使管理後台與會員中心（`member-web`）達到一致的狀態管理品質與安全標準。
