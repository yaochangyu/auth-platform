# 身分驗證與授權平台架構規劃書 (auth-platform)

本文件定義統一身分與授權平台（`auth-platform`）之整體架構、系統邊界、Domain Name 與工程策略，核心區分**一般使用者（User-Facing）**與**伺服器端（Server-Facing / M2M）**。

---

## 核心設計原則：一般使用者 vs 伺服器端

| 維度 | 一般使用者 (User-Facing) | 伺服器端 (Server-Facing / M2M) |
| :--- | :--- | :--- |
| **互動對象** | 終端會員（瀏覽器 SPA、App） | 後端服務、排程、第三方夥伴系統 |
| **身分核心** | 特定使用者身分（User Context） | 服務或租戶身分（Service / Machine Context） |
| **認證機制** | OAuth 2.0 Authorization Code + PKCE、OIDC | OAuth 2.0 Client Credentials、API Key、HMAC 簽章 |
| **憑證特質** | 短效 Access Token、隨登出失效 | 長效金鑰、配額控管、平順輪替 |
| **權限範圍** | 個人資料存取（User Scopes） | 系統功能與資料拋轉（Service Scopes） |
| **主要防護** | 重定向劫持防範、Token 洩漏防禦 | IP 白名單、重送攻擊防禦、頻率限制 |

---

## 一、API 端點切分原則

- **路由強制分離**：人用的 API 與伺服器用的 API 不共用業務路由，避免邏輯交雜引發越權存取（BOLA/IDOR）。
  - **用戶 API**：`/api/v1/user/...`（強制繫結當前登入者 ID）。
  - **伺服器 API**：`/api/v1/services/...`（依系統授權處理指定或批次請求）。
- **底層架構整合**：共用同一 API Gateway 與身分中介層，統一負責驗證分流。

---

## 二、系統模組與介面命名

| 模組 | 推薦命名 | 用途簡述 |
| :--- | :--- | :--- |
| **會員中心前台** | **已連結的應用程式** / **授權管理** | 會員檢視與撤銷已授權的第三方 App |
| **開發者平台** | **開發者平台** / **應用程式整合** | 外部夥伴自主申請 Client、API Key 與查閱規格 |
| **內部管理後台** | **憑證管理後台** / **授權審核中心** | 內部審核申請、核發金鑰、配置 IP 白名單 |

---

## 三、Domain Name 規劃

以主網域 `1111.com.tw` 為基準劃分：

### 1. 一般使用者網域
- **`member.1111.com.tw`**：會員前台（個人資料維護、已授權 App 管理）。
- **`account.1111.com.tw`** 或 **`auth.1111.com.tw`**：身分認證入口（OAuth 2.0 / OIDC 端點與互動登入頁）。

### 2. 伺服器端與夥伴網域
- **`api.1111.com.tw`**：API Gateway 統一請求入口。
- **`developer.1111.com.tw`**：外部夥伴入口（申請憑證、管理專案）。
- **`auth-admin.1111.com.tw`**：內部管理後台（限內網或 VPN）。

---

## 四、憑證與 API Key 管理機制

- **核發審核流程**：夥伴提出申請 → 內部審核並配置權限與 IP 白名單 → 系統發放金鑰。
- **安全防護三要素**：
  1. **單次檢視**：建立時僅顯示一次 Secret，資料庫只儲存雜湊值。
  2. **平順輪替**：支援新舊金鑰並存期，確保換 Key 服務不中斷。
  3. **防重送攻擊**：HMAC 簽章強制包含有效時間戳記（Timestamp）與隨機數（Nonce）。

---

## 五、代碼庫結構 (Monorepo)

統一採用單一儲存庫 `auth-platform`，依目錄職責明確隔離：

```text
auth-platform/
├── apps/
│   ├── identity-server/    # OAuth 2.0 / OIDC 核心身分服務
│   ├── member-web/         # 會員中心前台
│   ├── admin-portal/       # 內部憑證與審核後台
│   └── developer-portal/   # 開發者平台
└── packages/
    ├── shared-hmac/        # HMAC 數位簽章共用模組
    ├── auth-contracts/     # 共用資料結構與合約
    └── ui-components/      # 共用前端 UI 元件庫
```

---

## 六、CI/CD 自動化部署策略

- **目錄變更觸發（Path-based Triggers）**：各子系統配置獨立的 CI/CD Pipeline，僅在自身目錄異動時觸發建置與部署。
- **共用模組連動**：當 `packages/` 異動時，自動觸發相依服務之驗證與測試。
- **獨立交付**：各 App 獨立產出部署單元（如 Docker Image 或靜態網站），互不影響。
