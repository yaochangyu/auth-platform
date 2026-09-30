# ADR 0008：會員中心受保護 API 鑑權機制、原生 App 認證與用戶資料寫入邊界

## 狀態
已拍板 (Accepted) - 2026-09-30

## 背景
專案已完成 Phase 1（會員中心基礎與 Cookie 鑑權）與 Phase 2（OAuth 2.1 授權中心、開發者與管理平台）。目前 `member-api` 的受保護端點（個人資料、修改密碼、已授權應用管理）主要依賴 ASP.NET Core Identity Cookie（`.AspNetCore.Cookies`，網域 `.1111.com.tw`）進行身分識別。

隨著平台演進，出現了以下關鍵架構擴充需求：
1. **跨通道呼叫**：未來將有 1111 自家原生行動 App（iOS/Android）、其他第一方業務 Web，以及第三方生態夥伴需要調用會員 API。
2. **委託授權邊界**：第三方生態系統與行銷活動需要代表會員讀取或補充特定資料（如活動補填生日、完善檔案）。
3. **原生 App 安全登入**：原生行動 App 作為 OAuth 2.1 公用客戶端（Public Client），需要標準且具備 SSO 與憑據隔離的認證機制。
4. **即時作廢與公開端點防護**：如何在支援 Bearer Token 離線驗證的同時，維持既有的安全戳記（Security Stamp）防護，並防範開放 API 遭受惡意枚舉與爆破。

## 決策內容 (Decisions)

### 1. 雙軌鑑權機制 (Dual-Scheme Authentication)
`member-api` 同時註冊並支援兩種認證 Scheme：
- **`CookieAuthentication` (Default for Web)**：供第一方 `member-web` 瀏覽器端使用，維持既有無感單點登入與 HttpOnly 安全防禦。
- **`JwtBearer` (Default for App & APIs)**：供行動端 App、內部微服務與第三方持有者使用。以 `auth-server` 之 OIDC Discovery / JWKS 進行離線 RS256 驗證（對齊 ADR 0005）。

### 2. 客戶端信任分級與資料寫入邊界 (Client Tiering & Profile Write Policy)
針對會員資料的讀寫與帳號治理端點，落實嚴格的權限分級防護：
- **第一方客戶端 (First-Party Client)**：包含 `member-web`、官方 Native App 與 1111 旗下特定業務 Web（如行銷/復育活動站）。
  - 具備存取權限：個人檔案全欄位讀取、資料增量補填（`PATCH /api/v1/user/profile`，如補填生日/電話）。
  - **欄位級可變性策略 (Field Mutability Policies)**：
    - **自由變更欄位 (Mutable / User-Controlled)**：如學歷（高中升大學）、通訊地址、職業職稱、簡介等。完全尊重用戶自主決定，允許隨時透過第一方 Web/App 直接更新覆寫，不設任何鎖定。
    - **防弊與法定防護欄位 (Write-Once / Anti-Abuse)**：僅限「生日」（涉及生日禮金防弊、法定年齡）與身分證字號等核心屬性。此類欄位採「首次補填（Attribute Completion）放行、填寫後鎖定」原則；若資料庫已有值，活動端點禁止任意覆寫，須走會員中心專屬更正流程。
- **外部第三方夥伴 (Third-Party Partner)**：
  - 僅限讀取（Read-Only）：僅能於會員 Consent 後獲得 `profile` / `email` 讀取權限，打 `GET /api/v1/user/profile` 或 OIDC `/connect/userinfo`。
  - **完全禁絕寫入與帳號治理**：第三方 Token 絕對禁止存取任何寫入端點（`PUT`/`PATCH`）與安全治理端點（`POST /change-password`、`DELETE /authorized-apps/{clientId}`），違者回傳 `403 Forbidden`。
- **高敏感帳號治理端點 (Account Governance)**：
  - `POST /api/v1/user/change-password` 與 `DELETE /api/v1/user/authorized-apps/{clientId}` 強制綁定為第一方會話（Cookie）或內部專屬特權 Token。

### 3. 原生 App 採 RFC 8252 (OAuth 2.0 for Native Apps)
- **公用客戶端宣告**：Native App 在授權伺服器登記為 `Public Client`，禁止配發 Client Secret。
- **強制 PKCE 與 S256**：強制 `RequirePkce = true`，僅接受 `S256` 驗證碼挑戰。
- **系統原生安全瀏覽器**：App 登入強制叫起 iOS `ASWebAuthenticationSession` / Android `Chrome Custom Tabs` 轉導至 `auth-server` 登入頁，嚴禁 App 原生代碼碰觸明文帳密（落實 ADR 0004 禁絕 ROPC 原則）。
- **網域深層連結保護**：Redirect URI 強制採用官方 iOS `Universal Links` 與 Android `App Links`，驗證網域簽章，杜絕 Custom Scheme 授權碼攔截劫持。
- **公開業務流程原生存取**：註冊（Register）、信箱驗證（Verify Email）、忘記密碼（Forgot Password）、重設密碼（Reset Password）維持開放 Native App 呼叫 JSON API。

### 4. 全鏈路登出與撤銷機制 (Logout & Revocation)
- **RFC 7009 權杖撤銷**：App 使用者執行登出時，App 本機清除儲存之 Token，並向授權中心發送 `POST /connect/revocation` 撤銷其 Refresh Token。
- **安全戳記與全局註銷連動**：會員變更密碼或選擇「登出所有裝置」時，立即刷新資料庫之 `SecurityStamp`，既有 Cookie 瞬間作廢，授權中心亦連動廢止該會員名下所有發行中的 Refresh Token。

### 5. 業務層頻率限制 (Rate Limiting)
- 針對公開暴露給 App 與外部直接呼叫的端點（`register`、`forgot-password`、`verify-email`），在 ASP.NET Core 管道中掛載以 Client IP 與帳號識別為維度的 Rate Limiting 限制（例如重發驗證信每小時 3 次、密碼重設每分鐘 5 次），防止郵件炸彈與撞庫枚舉。

## 後果 (Consequences)
- **正面效益**：
  1. 架構兼顧了 Web 的無痛 SSO、App 的標準生態串接與第三方的最小授權安全。
  2. 解決了行銷活動補填資料的彈性需求，同時守護了會員核心檔案不可任意竄改的安全性。
  3. 杜絕了行動端反編譯洩漏 Secret 與惡意 App 側錄帳密的安全隱患。
- **承擔代價**：
  1. `member-api` 的授權邏輯需維護 Cookie 與 Bearer 的複合授權原則（Composite Policies）。
  2. 行動端開發需要整合 ASWebAuthenticationSession / Chrome Custom Tabs 與 Deep Links。
