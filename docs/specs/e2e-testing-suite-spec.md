# 全鏈路瀏覽器端到端 (E2E) 測試套件規格書 (E2E Testing Suite Spec)

## Problem Statement

目前 auth-platform 雖具備 315 項後端 BDD 跨層整合測試（以 WebApplicationFactory + Testcontainers 驗證 API 與資料庫行為），且在 `scripts/` 中保有以 curl 撰寫的 Bash 煙霧測試腳本，但缺乏**跨微前端（member-web、developer-web、admin-web）與認證伺服器（auth-server）真實連動的瀏覽器級端到端（E2E）自動化測試**。

這導致開發團隊無法自動化驗證跨網域 SSO 登入、Cookie 傳遞、OAuth 2.1 授權碼重定向、Consent 同意畫面、前端表單驗證、動態金鑰明文提示以及管理員審核操作等真實使用者操作旅程，每次重構微前端或後端認證管道時，仍需依賴繁瑣的手動點擊驗收，存在高風險的回歸盲區。

## Solution

引入以 **Playwright** 為核心的現代化跨瀏覽器端到端（E2E）自動化測試套件，涵蓋三套微前端應用程式與後端 API / 授權伺服器在 Docker Compose 容器環境下的全鏈路真實連動：
1. **統一 E2E 測試工程化架構**：建立獨立的 E2E 測試工程目錄與 `playwright.config.ts`，支援本地開發模式與 Docker 多容器網路環境切換，具備容器就緒檢查與一鍵執行能力。
2. **通訊虛擬端點黑箱整合（Mailpit & Smspit Clients）**：透過最高測試接縫（REST API）查詢虛擬郵件與簡訊驗證碼，不侵入系統內部狀態。
3. **全鏈路核心旅程覆蓋**：
   - 會員中心：雙軌註冊 ➔ OTP 驗證 ➔ SSO 登入 ➔ 個人檔案維護（含生日寫一次防弊）➔ 登出。
   - 開發者中心：SSO 身分延續 ➔ 建立 OAuth 應用 ➔ 發行 Client Secret 與 API Key ➔ 提交審核。
   - 管理者中心：管理員登入 ➔ 待審核清單 ➔ 審核核准/駁回 ➔ 緊急斷路停用 ➔ 稽核日誌 (Audit Logs) 查驗。
   - OAuth 2.1 授權流：第三方發起 PKCE 授權 ➔ 瀏覽器跳轉至 AuthServer ➔ 登入與 Consent 同意 ➔ 授權碼換票與 UserInfo 檢索。

## User Stories

1. As a new user, I want to register an account using my email on the member portal, so that I can receive an activation link or verification token.
2. As a new user, I want to activate my email account using the verification token, so that I can establish a valid member identity.
3. As a new user, I want to register an account using my mobile phone number, so that I have a dual-track registration option.
4. As a new user, I want to receive an SMS OTP via the mobile phone verification service, so that I can prove ownership of my phone.
5. As a registered member, I want to log in using my email and password, so that I can receive an SSO Session Cookie on `.1111.com.tw`.
6. As a registered member, I want to log in using my mobile phone and password, so that I can seamlessly access the platform via either identity.
7. As an authenticated member, I want to view my profile information, so that I can confirm my login state and identity details.
8. As an authenticated member, I want to incrementally update my profile fields (such as birthday) once, so that my personal details are accurate while preventing duplicate overwrites.
9. As an authenticated member, I want to log out from the member portal, so that my SSO session is revoked across the entire domain.
10. As a developer, I want to visit the developer portal and be automatically recognized via the existing SSO session, so that I do not need to re-enter credentials.
11. As a developer, I want to see my authenticated email and role displayed in the developer navigation bar, so that I am aware of my active context.
12. As a developer, I want to create a new OAuth application with specified redirect URIs and client types (Confidential vs Public), so that I can integrate my third-party service.
13. As a developer, I want to generate a new Client Secret and be presented with a clear plaintext copy warning, so that I can securely store my credential before it is permanently masked.
14. As a developer, I want to generate a new API Key with environment and scope configurations, so that I can perform machine-to-machine HMAC authentication.
15. As a developer, I want to submit my draft application for administrative review, so that its status transitions to Pending.
16. As a developer, I want to trigger a full-link logout from the developer portal, so that all active sessions and local authentication states are purged.
17. As an administrator, I want to log into the admin portal using administrative credentials, so that I can access governance functions.
18. As an administrator, I want to see my identity and administrator privileges in the admin navigation bar, so that I know I have system review authorization.
19. As an administrator, I want to view a list of applications waiting for review, so that I can process developer onboarding requests.
20. As an administrator, I want to approve a pending application, so that the OAuth client becomes active and capable of issuing tokens.
21. As an administrator, I want to reject a pending application with a specified reason, so that the developer can correct their submission.
22. As an administrator, I want to immediately suspend an active application in an emergency, so that all existing tokens, authorizations, and API keys are invalidated.
23. As an administrator, I want to inspect the platform audit logs, so that every review, status modification, and security action can be traced back to the actor and timestamp.
24. As a third-party app user, I want to initiate an OAuth 2.1 authorization code flow with PKCE, so that my authorization request is cryptographically protected against interception.
25. As a third-party app user, I want to be redirected to the centralized authorization server and prompted for consent, so that I can review and approve requested scopes.
26. As a third-party app user, I want to be redirected back to the registered callback URL with an authorization code upon granting consent, so that the third-party client can exchange it for tokens.
27. As a QA / CI engineer, I want the entire E2E test suite to execute automatically against the Docker Compose environment, so that regressions across the multi-service platform are detected before deployment.
28. As a QA / CI engineer, I want failed E2E tests to automatically capture screenshots, video recordings, and network traces, so that debugging transient or rendering failures is efficient.

## Implementation Decisions

- **E2E 測試框架選型**：採用 **Playwright (@playwright/test)**，具備優秀的跨網域、跨瀏覽器支援，原生支援多 Context、Cookie 隔離與高可靠性 auto-waiting。
- **測試接縫 (Seams) 策略**：
  - 最高外部接縫：真實瀏覽器 DOM 與 HTTP 流量，完全從使用者與外部客戶端視角出發。
  - 外部依賴隔離接縫：郵件與簡訊通訊依賴藉由現有的 **Mailpit REST API**（`http://localhost:8025/api/v1/messages`）與 **Smspit REST API**（`http://localhost:8026/api/v1/messages`）擷取 OTP 與 Token，保持系統黑箱。
- **Page Object Model (POM) 模式**：
  - 各前端（MemberPage, DeveloperPage, AdminPage, AuthorizeConsentPage）封裝高層動作與定位器，測試腳本僅描述使用者操作與斷言，避免 UI 細節變動導致測試脆弱（Fragile Tests）。
- **獨立測試目錄與 npm 腳本架構**：
  - 專案根目錄建立 `e2e/` 專案（含獨立 `package.json`），避免污染前端業務專案相依性。
  - 支援 `npm run test:e2e` 與 `npm run test:e2e:ui`。
- **跨網域與 SSO Session 處理**：
  - 利用 Playwright `baseURL` 與 `storageState` 或多分頁導航，模擬在同一瀏覽器上下文中訪問 `member.1111.com.tw:8080`、`developer.1111.com.tw:8092`、`admin.1111.com.tw:8093` 與 `auth.1111.com.tw:8091`。

## Testing Decisions

- **測試原則**：
  - 僅驗證外部可觀測行為（頁面渲染、導航重定向、文字與錯誤提示呈現、HTTP 回應狀態），嚴禁依賴內部私有變數或資料庫直接竄改。
  - 所有測試必須具備可重現性與獨立性，使用動態時間戳與隨機郵件/手機號碼建立獨立測試帳號，避免彼此干擾。
- **覆蓋模組**：
  - `apps/member-web`
  - `apps/developer-web`
  - `apps/admin-web`
  - `apps/auth-server`（Consent 與 Authorize 端點互動）
  - 後端 API 集群（透過前端真實 fetch / XHR 請求觸發）
- **先驗範例（Prior Art）**：
  - 既有 Bash 煙霧測試腳本（`scripts/smoke-test.sh`、`scripts/oauth-smoke-test.sh`、`scripts/phase3-smoke-test.sh`、`scripts/sms-smoke-test.sh`）中定義的業務操作鏈路與預期狀態碼。
  - 後端 BDD Gherkin 特性檔案（`Login.feature`, `Registration.feature`, `OAuthClient.feature`, `ApplicationReview.feature`）定義的業務規格。

## Out of Scope

- 後端單元測試與 API 整合測試（已有 315 項測試維持 100% 覆蓋）。
- 前端單元測試（Vitest 元件測試非本規格範疇）。
- 效能壓力測試（JMeter / k6 負載測試非本規格範疇）。
- 部署至公有雲之 CI/CD Pipeline 實作（本規格專注於本地與 Docker Compose 環境的一鍵執行套件）。

## Further Notes

- 執行前置需確保 Docker Compose 容器運行中，或提供自動拉起與健康檢查機制。
- 測試需相容於本機 `/etc/hosts` 映射或 Playwright 的額外 HTTP Headers / 代理設定。
