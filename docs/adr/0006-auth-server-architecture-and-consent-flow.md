# 0006. 授權伺服器架構設計、Vue SPA 同意流程與安全防護

- **狀態 (Status)**: 已採納 (accepted)
- **日期 (Date)**: 2026-09-29

## 背景脈絡 (Context)

在完成會員中心垂直切片後，系統進入授權伺服器（Auth Server / Identity Provider）實作階段。本階段需確定授權伺服器在資料庫層級的實體劃分、Vue SPA Consent 互動流程、權杖生命週期與簽章金鑰管理，以保障高安全性與最佳開發體驗。

## 決策 (Decision)

經深入盤問與架構權衡，確立以下五大實作規範：

### 1. 資料庫與 DbContext 邊界
- **共用資料庫，獨立 DbContext**：Auth Server 與既有 MemberApi 共享同一個 PostgreSQL 資料庫，但各自維護獨立的 EF Core DbContext（`AuthServerDbContext` vs `MemberApiDbContext`）與個別的 Migration 記錄。
- **解耦效益**：避免單一 DbContext 實體過度膨脹，同時兼顧跨表查詢效能與本機單一 PostgreSQL 連線的維運輕量性。

### 2. Vue SPA Consent 授權流程與安全防護
- **流程編排**：
  1. 第三方客戶端發起 `/connect/authorize`。
  2. 若客戶端為受信任的第一方應用（First-party Client），直接執行自動同意（Auto-Consent）。
  3. 若為第三方客戶端且尚未取得授權，Auth Server 以 ASP.NET Core Data Protection 將授權上下文加密封裝為短效（5 分鐘）、單次使用的 **Consent Ticket**（`consent_id`），並以 302 重定向至 `member-web` 的 `/oauth/consent?consent_id=...`。
  4. `member-web` 前端 Vue SPA 呼叫 `/api/v1/oauth/consent/{consent_id}` 取得應用程式名稱與請求 Scope 清單，使用 shadcn-vue 介面供會員確認。
  5. 會員點選同意/拒絕後，POST 回 Auth Server，Auth Server 驗證無誤後簽發 Authorization Code，並回傳目標 Redirect URI（帶 `code` 與 `state`），由前端跳轉回客戶端。
- **防竄改防線**：授權請求的所有敏感參數（Redirect URI、Scope、Code Challenge）全程由後端加密簽章保護，前端無法偽造或竄改。

### 3. Client 註冊與種子資料驅動
- 在專案內實作 `ClientSeeder`，於服務啟動時自動檢查並寫入預設的 OAuth Client（例如 `member-web-spa`、`demo-third-party-app`），確保本機與 BDD 自動化測試（Testcontainers）具備開箱即用的測試資料。

### 4. 權杖生命週期與安全輪替 (Rotation)
- **Access Token**：生命週期短效（15 分鐘），非對稱簽章 JWT。
- **Refresh Token**：30 天滑動過期，且強制啟用 **Refresh Token Rotation**（每次使用換取新 Token 時舊 Token 立即作廢；若偵測到已作廢的 Refresh Token 再次被使用，立即撤銷整個授權許可家族以防禦權杖洩漏攻擊）。

### 5. 金鑰持久化與標準協定端點
- **金鑰管理**：開發與容器環境自動產生並保存 PEM 格式非對稱私鑰（`auth-signing-key.pem`），避免重啟導致既有 JWT 與 JWKS 快取失效；正式環境支援環境變數或 Key Vault 注入。
- **端點路由**：全面採用標準 RFC / OpenIddict 慣例路徑（`/.well-known/openid-configuration`、`/.well-known/jwks.json`、`/connect/authorize`、`/connect/token`、`/connect/userinfo`、`/connect/revocation`）。
- **Claims 映射**：僅提供標準 OIDC 核心 Claims（`sub`、`email`、`email_verified`、`nickname`、`updated_at`）。

### 6. Client 鑑權、拒絕處置與登出邊界
- **Client 鑑權支援**：同時支援 Public Client（純 PKCE，無 client_secret）與 Confidential Client（支援 client_secret_post 與 client_secret_basic）。
- **拒絕授權處置**：遵循 RFC 6749，會員在 Consent 介面點選「拒絕」時，系統 302 重定向回客戶端 `redirect_uri?error=access_denied&state=...`。
- **登出邊界收斂**：第一期專注於 Token Revocation 與 Session Cookie 撤銷，暫不實作跨系統 Front-channel / Back-channel 全域單點登出。


## 後果與權衡 (Consequences)

- **安全性極大化**：強制 PKCE、Refresh Token Rotation、Consent Ticket 防竄改與非對稱金鑰簽章，符合現代 OAuth 2.1 最高安全標準。
- **使用者體驗與品牌一致**：授權同意畫面維持純 Vue 3 + shadcn-vue 精緻 UI，並支援第一方內部系統的免手動跳轉（Silent SSO）。
- **實作複雜度**：相較於純 Razor Pages，Vue SPA Consent 流程需額外處理前後端 Consent Ticket 的驗證與交換端點。
