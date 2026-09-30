# [Spec] 會員中心受保護 API 雙軌鑑權、Native App 授權與欄位增量補填 (Phase 3)

## 問題陳述 (Problem Statement)

目前會員中心 API 服務的受保護端點（個人檔案讀寫、密碼變更、已授權應用管理）僅掛載基於瀏覽器的會話 Cookie 鑑權（網域限定於主網域），專門服務於第一方 Web 網站。

隨著平台朝向多端與開放生態演進，浮現以下關鍵阻礙：
1. **行動端整合障礙**：官方原生行動 App（iOS / Android）無法且不應仰賴瀏覽器會話 Cookie 作為微服務鑑權憑證，缺乏標準 OAuth 2.1 授權碼與 PKCE 登入換票機制。
2. **委託授權通道缺位**：第三方生態夥伴與內部第一方業務站（如行銷活動平台、求職推薦微服務）在獲得會員授權後，無法持標準 Bearer Access Token 調用會員中心受保護 API。
3. **資料覆寫風險與缺乏增量更新**：現有個人檔案僅提供全量覆寫端點，當活動網站（例如會員復育活動、生日領券）僅需引導用戶補充特定資料（如補填生日、更新學歷）時，無法進行安全增量補填（`PATCH`），極易引發非預期覆寫。同時，未對「防弊特徵欄位（如生日）」與「自由流動欄位（如學歷升級）」實施差異化防護。
4. **端點邊界與權限分級缺失**：高敏感帳號治理操作（修改密碼、撤銷其他應用程式授權）若無差別開放給持有 Token 的客戶端，存在第三方惡意越權篡改的嚴重安全隱患。

---

## 解決方案 (Solution)

全面依據 **ADR 0008**，為會員中心 API 升級至**雙軌鑑權架構（Dual-Scheme Authentication）**，並建立原生行動端授權規範與欄位級可變性策略：

1. **雙軌鑑權中介層**：
   - 同時掛載會話 Cookie 與 Bearer JWT 驗證機制。
   - 第一方 Web 維持無痛 HttpOnly Cookie 驗證與單點登入體驗。
   - 行動端 App 與經授權的客戶端透過 HTTP 標頭 `Authorization: Bearer <token>` 存取，由 API 服務作為資源伺服器（Resource Server）透過授權伺服器之 JWKS 進行離線 RS256 驗證。
2. **原生 App 授權與 RFC 8252 合規**：
   - 行動 App 在授權伺服器註冊為公用客戶端（Public Client），禁止核發 Client Secret。
   - 強制套用授權碼模式搭配 PKCE（S256），登入流程透過系統原生安全瀏覽器元件導向授權中心登入頁，嚴禁 App 原生代碼碰觸明文帳密。
   - 重定向網域強制採用官方 Universal Links 與 App Links 驗證，杜絕授權碼劫持。
3. **客戶端分級與欄位級可變性策略 (Field Mutability Policies)**：
   - 提供增量屬性補填端點（`PATCH /api/v1/user/profile`）。
   - **自由變更欄位（學歷、通訊地址、職稱）**：完全由用戶自主決定，允許隨時透過第一方客戶端覆寫更新。
   - **防弊與法定特徵欄位（生日）**：採 Write-Once（填寫後鎖定）原則，資料庫未填時允許補填，已有值則禁止活動端點隨意覆寫。
   - **高敏感帳號治理端點（變更密碼、撤銷已授權應用）**：強制限定為第一方會話或內部特權身分，第三方 Token 一律拒絕（403 Forbidden）。
4. **全鏈路撤銷與安全連動**：
   - 支援 RFC 7009 權杖撤銷，App 登出時背景主動撤銷 Refresh Token。
   - 密碼變更或重設時立即刷新安全戳記（Security Stamp），全平台 Cookie 瞬間失效，授權中心連動廢止既有 Refresh Token。
5. **公開端點業務層限速**：
   - 針對開放給 App 與外部直接調用之公開流程（註冊、忘記密碼、信箱驗證），配置業務層 Rate Limiting。

---

## 使用者故事 (User Stories)

1. 作為一名使用官方 iOS/Android App 的會員，我想要在 App 點擊登入時喚起系統原生安全瀏覽器完成驗證，以便在不暴露明文密碼給 App 的前提下安全登入。
2. 作為一名在瀏覽器已經登入過的會員，當我在官方 App 發起登入時，我希望系統能識別既有安全會話實現無感快速跳轉登入，以避免重複輸入帳密。
3. 作為一名已登入 App 的會員，我想要使用 App 瀏覽我的個人資料，App 能以 Bearer Access Token 順利取得資料，以便確認檔案無誤。
4. 作為一名參加行銷復育活動的會員，我想要在活動介面上補填我的生日領取折價券，系統能增量寫入我的生日，使我不必手動到會員中心完整重新編輯整份資料。
5. 作為一名已經填寫過生日的會員，若我在活動網站嘗試再次修改生日，系統應拒絕覆寫並提示已設定，以防止惡意刷取不同月份活動優惠。
6. 作為一名剛取得更高學位的會員，我想要在第一方 App 或 Web 上將最高學歷從高中更新為大學，系統應直接放行並更新，完全尊重我的自主維護權利。
7. 作為一名會員，我想要自由更新我的通訊地址與目前職稱，系統應直接更新生效而不設置任何阻擋。
8. 作為一名已獲得會員授權的第三方應用程式，我想要持具有 profile scope 的 Access Token 讀取該會員的基本資料，以便提供個性化服務。
9. 作為一名第三方應用程式開發者，當我試圖呼叫變更密碼 API 時，系統應回傳 403 Forbidden，以防止惡意或受劫持的第三方應用篡改會員安全憑據。
10. 作為一名第三方應用程式開發者，當我試圖呼叫解除其他已授權應用端點時，系統應回傳 403 Forbidden，以防止惡意競爭排他或越權操控。
11. 作為一名在 App 上點擊登出的會員，我希望 App 能在背景向授權中心作廢我的 Refresh Token，以確保手機即使遺失該 Token 也無法再換取新憑證。
12. 作為一名懷疑帳號外洩而透過會員中心變更密碼的會員，我希望手機 App 與所有已登入裝置在短效 Token 到期後無法自動換票，並強制重新登入，以確保帳號安全。
13. 作為一名惡意攻擊者，當我嘗試透過未註冊之自訂 URL Scheme 竊取公用客戶端的授權碼時，系統應透過 Universal Links / App Links 網域所有權機制防阻重定向。
14. 作為一名平台維運工程師，當有惡意爬蟲對公開端點進行大量請求時，我希望系統能以 IP 與帳號維度實施頻率限制，以保護底層服務正常運作。
15. 作為一名使用 member-web 的第一方瀏覽器會員，我希望能繼續保有原生的 HttpOnly Cookie 體驗與操作，不受雙軌中介層引進的任何衝擊。

---

## 實作決策 (Implementation Decisions)

- **雙軌鑑權架構 (Dual-Scheme Architecture)**：
  - 在 API 服務的依賴注入中，同時註冊 `CookieAuthenticationDefaults.AuthenticationScheme` 與 `JwtBearerDefaults.AuthenticationScheme`。
  - 配置自訂複合授權策略（Composite Authorization Policy）或 Policy Scheme，依據 HTTP Header 是否存在 `Bearer` 自動分流至對應驗證處理常式。
- **資源伺服器驗證 (Resource Server Validation)**：
  - `JwtBearer` 設定 `Authority` 指向授權伺服器，透過 OIDC Discovery 端點快取並定期輪替 JWKS 公鑰。
  - 以離線 RS256 驗證數位簽章、Token 有效期（Lifetime）與 Audience / Issuer，維持極致驗證效能。
- **原生 App 公用客戶端規範 (Public Client Specification)**：
  - 授權伺服器針對客戶端類型為 `Public` 的應用，禁止配發 Client Secret。
  - 強制啟用 PKCE 檢驗，要求授權請求必須包含 `code_challenge` 與 `code_challenge_method=S256`。
  - 重定向網域強制要求 HTTPS scheme（搭配 iOS Universal Links / Android App Links）。
- **增量更新與欄位可變性策略實作**：
  - 新增 `PATCH /api/v1/user/profile` 端點，支援可選欄位增量更新契約。
  - 商業驗證中介層檢核：
    - `Education`、`Address`、`JobTitle`：允許直接寫入與更新。
    - `Birthday`：若當前資料庫紀錄已具備值，且請求包含相異生日值，則中斷並回傳 RFC 7807 `409 Conflict` 錯誤（生日已設定，禁止重複覆寫）。
- **高敏感端點隔離防護**：
  - `POST /api/v1/user/change-password` 與 `DELETE /api/v1/user/authorized-apps/{clientId}` 掛載專屬授權策略：僅允許認證方案為 Cookie 之請求，或具備第一方內部專屬標記之 Token；任何第三方持有的 Bearer Token 均禁止存取。
- **全鏈路權杖撤銷與安全戳記連動**：
  - 授權伺服器暴露標準 RFC 7009 `POST /connect/revocation` 端點。
  - 密碼變更或重設時刷新資料庫 `members.security_stamp`，同時發送領域事件通知授權伺服器標記撤銷該會員的所有有效 Refresh Token。
- **業務層 Rate Limiting**：
  - 採用 ASP.NET Core 內建 `RateLimiter` 中介層，以 Client IP 與端點建立 Sliding Window 限速規則。

---

## 測試決策 (Testing Decisions)

- **單一最高測試接縫政策 (Single High Seam Policy)**：
  - 後端測試嚴格收斂在唯一的最高接縫：`WebApplicationFactory<Program>` HTTP 端點層級 + `Testcontainers` (PostgreSQL)。
  - 不撰寫孤立單元測試或內部依賴 Mock；所有測試皆以真實 HTTP 請求調用端點，並驗證實體資料庫狀態與 HTTP 回應。
- **BDD 規範與事前審核**：
  - 針對上述 User Stories 撰寫 Given-When-Then 自動化驗收情境。
  - 測試案例編寫完成後，必須先讓使用者審核確認，方可開始進入程式碼實作。
- **涵蓋關鍵驗收情境**：
  1. **雙軌鑑權**：同一端點（`GET /api/v1/user/profile`）分別使用 Cookie 與合規 Bearer Token 調用均能成功驗證。
  2. **高敏感端點防禦**：使用第三方 Bearer Token 呼叫 `change-password` 端點時，精確斷言回傳 403 Forbidden。
  3. **欄位可變性防護**：呼叫 `PATCH` 更新學歷成功；未填生日者補填生日成功；已填生日者再次嘗試補填不同生日斷言 409 Conflict。
  4. **PKCE 公用客戶端授權碼流程**：測試原生 App 授權碼交換之 PKCE 驗證與 Token 核發。
  5. **權杖撤銷連動**：測試密碼異動後，舊會話與 Refresh Token 全鏈路失效。

---

## 明確排除範圍 (Out of Scope)

- 手機號碼與簡訊 SMS OTP 驗證流程（已完整記錄於 Issue #21 與 `docs/specs/mobile-phone-sms-otp-spec.md`，排定為獨立切片）。
- 自建全新的 Identity Provider 核心（全面沿用既有 `auth-server` 與 OpenIddict 成果）。
- 第三方社群登入整合（Google, Apple, Facebook Sign-in）。
- 行動端 App 原生 UI 與 Swift/Kotlin 程式碼實作（此規格聚焦於後端 API、授權規範與 OpenAPI 契約）。

---

## 補充說明 (Further Notes)

- 本規格嚴格遵守與擴充以下既有成果：
  - [ADR 0001：BDD 測試與 Testcontainers](file:///home/yao/projects/auth-platform/docs/adr/0001-bdd-and-testcontainers-for-api-testing.md)
  - [ADR 0002：Security Stamp 會話註銷](file:///home/yao/projects/auth-platform/docs/adr/0002-security-stamp-for-session-revocation.md)
  - [ADR 0004：OAuth 2.0 瀏覽器重定向機制與 Consent 責任邊界](file:///home/yao/projects/auth-platform/docs/adr/0004-oauth-redirection-and-consent-boundary.md)
  - [ADR 0005：OpenIddict 框架與 RS256 JWT / JWKS 權杖架構](file:///home/yao/projects/auth-platform/docs/adr/0005-openiddict-framework-and-jwt-jwks-tokens.md)
  - [ADR 0008：會員中心受保護 API 鑑權機制、原生 App 認證與用戶資料寫入邊界](file:///home/yao/projects/auth-platform/docs/adr/0008-member-api-protection-and-native-app-auth.md)
  - [CONTEXT.md 統一領域語言](file:///home/yao/projects/auth-platform/CONTEXT.md)
