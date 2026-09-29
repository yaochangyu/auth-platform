# 開發者後台規格說明書 (Developer Portal Specification)

- **狀態 (Status)**: 已核准 (Approved)
- **更新日期**: 2026-09-29
- **對應模組**: 開發者端為 `apps/developer-api`（後端）/ `apps/developer-web`（前端）；管理員端為 `apps/admin-api` / `apps/admin-web`（Issue #19）。ADR 0007 原稱 `auth-admin-*`，實際依 Issue #16、#19 命名

---

## 1. 核心定位與權限模型 (Positioning & Access Control)

開發者後台（Developer Console）專供各產品線工程師與合作夥伴自主管理「自己所擁有（Application Ownership）」之應用程式。

- **登入鑑權 (Dogfooding)**：
  - 前端以標準 OAuth 2.1 授權碼模式向 `auth-server` 登入。
  - 會員登入後取得 Access Token（包含 `sub: MemberId` 與 `role: "developer"` 或 `role: "admin"`）。
- **數據隔離邊界**：
  - 開發者（Developer）僅能檢視與維護自身 `MemberId` 建立的專案與憑證。
  - 系統管理員（Admin）具備全域檢視、審核與緊急切斷權力。

---

## 2. 功能模組規格 (Functional Modules)

### 模組 1：應用專案管理 (Application Lifecycle)
1. **建立專案**：
   - 必填欄位：應用程式名稱（`name`）、簡介（`description`）、聯絡窗口 Email。
   - 選填欄位：Logo 網址、官網首頁。
   - 系統自動產生唯一的 `ClientId`（UUID 或具可讀性之 slug）。
2. **專案列表**：
   - 展現登入者擁有的所有應用程式卡片或清單，標示運行狀態（`Active` 啟用、`PendingReview` 審核中、`Suspended` 已停用）。

---

### 模組 2：OAuth 2.1 客戶端設定 (Interactive Client Configuration)
1. **客戶端類型**：
   - `Public Client`：純前端 Web SPA 或行動原生 App，強制啟用 PKCE，不核發 Client Secret。
   - `Confidential Client`：具備安全後端的 Web 應用，支援 Client Secret 鑑權。
2. **網址白名單設定**：
   - **Redirect URIs**：授權回呼網址清單（支援多組，嚴格匹配）。
   - **Post Logout Redirect URIs**：登出後導向網址清單。
3. **存取範疇 (Scopes) 配置**：
   - 勾選該應用被允許請求的權限（如 `openid`, `profile`, `email`）。
4. **Client Secret 憑據生命週期**：
   - **建立金鑰**：產生高熵隨機 Secret，**前端僅明文展示一次**（附帶一鍵複製提醒），資料庫僅保存 SHA-256 雜湊。
   - **雙金鑰平滑輪替 (Graceful Rotation)**：支援產生新 Secret 並保留舊 Secret 一段過渡時間（如 24 小時），避免線上服務換票中斷。
   - **手動作廢**：遇洩漏風險可立即作廢特定 Secret。

---

### 模組 3：Server-to-Server (M2M) 憑據管理
1. **API Key 發行**：
   - 前綴格式：`ak_live_xxxxxxxx...`（正式機） / `ak_test_xxxxxxxx...`（測試機）。
   - 前端僅展示一次，資料庫僅存前 8 碼明文與全文單向雜湊。
   - 支援指定 Scopes 權限範圍與過期時間（Expiration Date）。
2. **雲端無固定 IP 安全防護**：
   - 支援 **Client Credentials Grant**（M2M Token 交換）。
   - 支援 **HMAC Request Signature**：配置 HMAC-SHA256 密鑰，呼叫端對 Method、Path、Timestamp 與 Body 進行簽章防偽（防重放與防中途竄改）。

---

### 模組 4：授權數據與串接指引 (Overview & Quickstart)
1. **授權會員統計**：
   - 顯示目前已獲得多少真實會員授權（Active Grants 總數）。
2. **串接指引與端點**：
   - 一鍵複製 Discovery URL (`/.well-known/openid-configuration`)、JWKS URL (`/.well-known/jwks.json`)。
   - 提供標準 Code Snippet（cURL, C#, TypeScript, Python）示範 PKCE 換票與 HMAC 簽章計算。

---

### 模組 5：操作日誌 (App Activity Log)
- 記錄該應用專案的憑據變更歷史（何時產生 Secret、輪替 API Key、修改 Redirect URI）。

---

## 3. 前端介面與兩級導航架構 (UI/UX Structure)

前端採用 Vue 3 + Vite + Tailwind CSS + shadcn-vue，設計兩級導航：

```
/apps
  ├── 應用列表（我的應用程式、+ 建立新應用）
/apps/:appId
  ├── 概覽與串接指引 (Overview & Quickstart)
  ├── OAuth 2.1 設定 (OAuth Configuration - Redirect URIs, Scopes, Client Secrets)
  ├── 機器存取憑據 (M2M Credentials - API Keys & HMAC Signatures)
  ├── 授權會員名單 (Connected Members)
  └── 專案活動記錄 (Activity Logs)
```
