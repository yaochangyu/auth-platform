# 統一身分與授權平台 (Auth Platform)

統一身分與授權平台（`auth-platform`）是遵循現代 **OAuth 2.1** 與 **OpenID Connect (OIDC)** 核心規範所建構之高安全性、雲原生身分識別中心（Identity Provider, IdP）與微服務授權平台。

本專案支援全生命週期身分與權限管理：包含自然人會員中心（SSO 單點登入）、公開授權伺服器（PKCE、JWKS、權杖輪替）、開發者自助服務控制台（Dogfooding 登入、應用管理、雙金鑰平滑輪替）、無固定 IP 伺服器對伺服器（S2S HMAC 簽章驗證）、以及系統管理員全域審查與緊急斷路器。

---

## 專案服務拓撲 (System Architecture & Topology)

本專案採用現代解耦之多服務架構，共包含 **8 大服務容器**：

| 服務模組 | 容器名稱 | 公開埠號 / 模擬網域 | 技術棧 | 職責與架構邊界 |
|---|---|---|---|---|
| **核心資料庫** | `postgres` | `5432` | PostgreSQL 17 | 實體共享資料庫，各服務獨立維護 Migration 歷程表記錄；內建防篡改觸發器。 |
| **會員中心** | `member-api`<br>`member-web` | **`8080`**<br>`member.1111.com.tw:8080` | .NET 10 Web API<br>Vue 3 + Tailwind + Nginx | 自然人註冊、Email 驗證、防爆力破解帳號鎖定、密碼重設；核發主網域 SSO Cookie。 |
| **授權中心** | `auth-server` | **`8091`**<br>`auth.1111.com.tw:8091` | OpenIddict + .NET 10 | OAuth 2.1 發號核心：強制 PKCE、JWKS 非對稱驗證、換票、Refresh Token 輪替、UserInfo。 |
| **開發者後台** | `developer-api`<br>`developer-web` | **`8092`**<br>`developer.1111.com.tw:8092` | .NET 10 Web API<br>Vue 3 + shadcn-vue + Nginx | 應用專案 CRUD (Application Ownership)、OAuth 客戶端設定、雙金鑰過渡輪替、API 金鑰。 |
| **管理員後台** | `admin-api`<br>`admin-web` | **`8093`**<br>`admin.1111.com.tw:8093` | .NET 10 Web API<br>Vue 3 + shadcn-vue + Nginx | 限定 `role: admin` 存取，全域應用審核、一鍵斷路器 (Circuit Breaker)、不可篡改 Audit Log。 |
| **共用程式庫** | `apps/auth-shared` | *(Class Library)* | .NET 10 (LTS) | 共享 OpenIddict 資料模型、通用 HMAC-SHA256 請求簽章中介軟體。 |

---

## 核心安全亮點 (Security & Architectural Highlights)

1. **OAuth 2.1 PKCE 強制**：
   - 僅允許 `code_challenge_method=S256`，嚴格校驗 `code_verifier`；全面廢除 Implicit 模式與密碼模式。
2. **主網域免密單點登入 (Silent SSO)**：
   - `member-api` 與 `auth-server` 透過共用 ASP.NET Core Data Protection 金鑰目錄與應用名稱，解密主網域 Session Cookie，並即時比對資料庫 `SecurityStamp`，達成跨站免密無縫單點登入。
3. **5 分鐘加密 Consent Ticket 防竄改**：
   - 第三方應用需會員手動同意時，Auth Server 將授權上下文加密封裝為短效、單次使用的 `consent_id`，302 跳轉至 Vue SPA。Scopes 嚴格限定只能縮減不可擴張。
4. **Refresh Token Rotation (RTR) 輪替攻防**：
   - Access Token 為短效 15 分鐘 RS256 JWT；Refresh Token 為 30 天滑動效期。每次換票舊 Token 立即失效；若偵測到重放舊 Token，系統自動觸發**「授權家族全面撤銷 (Family Revocation)」**。
5. **雲端無固定 IP 伺服器對伺服器 (S2S) 防護**：
   - 支援 Client Credentials Grant 換票。
   - 支援 API Key（前綴 `ak_live_...` / `ak_test_...`）搭配 **HMAC-SHA256 請求簽章**（Method + Path + Timestamp ±5m 防重放 + Body 雜湊防竄改，採固定時間比對防時序攻擊）。
6. **一鍵緊急斷路器 (Circuit Breaker) 與不可篡改 Audit Log**：
   - 管理員停用涉嫌濫用之 App 時，單一資料庫交易內列級鎖定，並整批撤銷該 App 所有 Token、授權紀錄與 API Key。
   - PostgreSQL 觸發器保證 `audit_logs` 表嚴禁任何 `UPDATE`、`DELETE` 或 `TRUNCATE`，杜絕銷毀證據可能。

---

## 快速上手 (Quick Start)

### 方式 A：Docker Compose 一鍵啟動全套服務（推薦）

專案根目錄已配置好全部容器編排、健康檢查與 Data Protection 金鑰持久化：

```bash
# 啟動全部 8 個容器（PostgreSQL、4 組 API、3 座前端 SPA）
docker compose up -d --build

# 檢查所有服務運行狀態與健康指標
docker compose ps
```

啟動完成後，各站預設存取位址（可透過 `--resolve` 或在 `/etc/hosts` 指向 `127.0.0.1` 訪問）：
- 會員中心：`http://member.1111.com.tw:8080`
- 授權伺服器：`http://auth.1111.com.tw:8091`
- 開發者後台：`http://developer.1111.com.tw:8092`
- 管理員後台：`http://admin.1111.com.tw:8093`

---

### 方式 B：本機開發與測試

#### 先決條件
- **.NET 10 SDK** (LTS)
- **Node.js 20+** 與 **npm**
- **Docker**（供 BDD 測試套件啟動 Testcontainers 測試資料庫）

#### 執行全套自動化 BDD 測試（245 項）
整合測試採用最高測試接縫（Single High Seam Policy），透過 Testcontainers 自動拉起真實 PostgreSQL 容器驗證業務情境：

```bash
dotnet test AuthPlatform.slnx
```

#### 執行全鏈路端到端 Smoke Test（65 項驗證）
在 Docker Compose 啟動狀態下，直接執行全流程自動化煙霧測試腳本（模擬真實註冊 ➔ 登入 ➔ 建立 App ➔ PKCE 授權 ➔ Consent ➔ 換票 ➔ UserInfo ➔ HMAC 簽章 ➔ 斷路切斷 ➔ 觸發器防篡改）：

```bash
./scripts/oauth-smoke-test.sh
```

---

## 規格文件與架構指南 (Documentation & Specs)

- 🌟 **系統全景規格與架構白皮書（人類可讀 HTML 文件）**：
  - [`docs/auth-platform-spec-guide.html`](docs/auth-platform-spec-guide.html)（專案內）
- 📖 **端到端驗收與啟動手冊**：
  - [`docs/oauth-e2e-verification.md`](docs/oauth-e2e-verification.md)
  - [`docs/specs/developer-portal-spec.md`](docs/specs/developer-portal-spec.md)
- 📐 **OpenAPI 3.0 契約定義**：
  - 會員中心：[`docs/specs/member-api-v1.yaml`](docs/specs/member-api-v1.yaml)
  - 開發者後台：[`docs/specs/developer-api-v1.yaml`](docs/specs/developer-api-v1.yaml)
  - 管理員後台：[`docs/specs/admin-api-v1.yaml`](docs/specs/admin-api-v1.yaml)
- 🏛️ **架構決策記錄 (Architecture Decision Records, ADR)**：
  - 位於 [`docs/adr/`](docs/adr/)，完整記錄 ADR 0001 至 ADR 0008 之技術選型與取捨權衡。
- 📚 **領域模型與統一語言**：
  - 詳見 [`CONTEXT.md`](CONTEXT.md)。

---

## 未來藍圖與待辦功能 (Roadmap / Backlog)

以下功能已完成架構設計與決策（見 [ADR 0008](docs/adr/0008-member-api-protection-and-native-app-auth.md)），保留於 Backlog 待後續排程實作：

- 📱 **手機號碼與簡訊 SMS OTP 驗證**（詳見 [`docs/specs/mobile-phone-sms-otp-spec.md`](docs/specs/mobile-phone-sms-otp-spec.md)）：
  - 資料庫擴充手機欄位與唯一索引。
  - SMS Transactional Outbox 派發機制與 OTP 驗證端點。
  - 支援「Email 或 手機號碼」雙軌識別登入。
- 🛡️ **Member API 雙軌鑑權（Dual-Scheme Authentication）**：
  - 為 Native App 與第三方提供 Bearer JWT 離線驗證通道。
  - 第一方客戶端資料增量補填（`PATCH /api/v1/user/profile`）與防弊欄位 Write-Once 保護。

