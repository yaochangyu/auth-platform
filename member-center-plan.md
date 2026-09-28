# 會員中心規劃書 (member-center-plan.md)

**計畫時間**: 2026-09-28 14:43 GMT+8  
**進度**: [計畫中]

---

## 一、計畫背景與目標

本計畫旨在構建統一身分與授權平台（`auth-platform`）中專屬於終端會員的**會員中心模組**。
涵蓋後端 API（`apps/member-api`）與前端網頁介面（`apps/member-web`），提供穩定、安全、易維護之會員基礎功能，並為未來的 OAuth 2.0 / OIDC 身分授權提供 SSO 登入基礎。

---

## 二、技術選型與核心決策

| 領域 | 決策項目 | 選定方案與說明 |
| :--- | :--- | :--- |
| **API 開發流程** | 開發模式 | **API First**：先撰寫 OpenAPI 規格 (YAML/JSON)，嚴格依照契約驅動前後端實作與測試。 |
| **後端技術** | 執行環境與框架 | **.NET 10 (LTS) / ASP.NET Core Web API**（遵循 `yaochangyu/api.template` 規則）。 |
| **後端架構** | 組織風格 | **Controller 架構 + Clean Architecture**（Controller -> Application Service -> Repository）。 |
| **資料庫** | 資料持久化 | **PostgreSQL**，搭配 Entity Framework Core (EF Core) 進行資料存取與 Migration。 |
| **身分 Session** | 驗證與狀態維護 | **Cookie Authentication (HttpOnly)**，綁定主網域 `.1111.com.tw`，兼具高安全性與跨子網域 SSO 支援。 |
| **帳號憑證** | 識別與重設 | **Email 為主帳號**，搭配 Email 驗證碼 / 重設密碼安全連結。 |
| **前端技術** | 前台技術棧 | **Vue 3 + Vite + TypeScript + Tailwind CSS + shadcn-vue**，搭配 Vue Router 與 Pinia。<br>全面遵循 **`vue-best-practices`** 規範（Composition API `<script setup lang="ts">`、薄路由視圖、Composables 抽離、明確契約）。 |
| **專案邊界** | Monorepo 路徑 | 後端：`apps/member-api/`，前端：`apps/member-web/`。 |

---

## 三、功能範疇與 API 規格 (API First)

### 1. 核心功能矩陣

```text
會員中心
├── 註冊 (Register)
│   ├── 輸入 Email、密碼、確認密碼
│   └── 發送驗證信 / 啟用帳號
├── 登入 (Login)
│   ├── Email + 密碼驗證
│   ├── 寫入 HttpOnly SSO Cookie (.1111.com.tw)
│   └── 支援 returnUrl 轉址（供未來 OAuth authorize 跳回）
├── 登出 (Logout)
│   └── 清除 Session Cookie
├── 忘記密碼 (Forgot Password)
│   └── 輸入 Email，產生安全 Token 並發送重設密碼信
├── 重設密碼 (Reset Password)
│   └── 驗證重設 Token 有效性，輸入並儲存新密碼
├── 變更密碼 (Change Password)
│   └── 已登入狀態下驗證舊密碼並更新密碼
└── 會員個人檔案 (Profile)
    └── 檢視基本資料、已連結的第三方授權應用程式 (Connected Apps)
```

### 2. OpenAPI 預計定義之端點契約
* `POST /api/v1/auth/register` - 會員註冊
* `POST /api/v1/auth/verify-email` - 驗證啟用 Email
* `POST /api/v1/auth/login` - 會員登入（發送 Auth Cookie）
* `POST /api/v1/auth/logout` - 會員登出
* `POST /api/v1/auth/forgot-password` - 發送忘記密碼通知（申請重設 Token）
* `POST /api/v1/auth/reset-password` - 驗證 Token 並執行重設密碼
* `GET  /api/v1/member/profile` - 取得個人檔案與登入狀態
* `PUT  /api/v1/member/password` - 已登入狀態變更密碼
* `GET  /api/v1/member/connected-apps` - 查詢已授權的第三方應用程式清單

---

## 四、資料模型設計 (PostgreSQL)

### 1. `members` (會員基本表)
- `id` (UUID, PK)
- `email` (VARCHAR, Unique, Indexed)
- `password_hash` (VARCHAR, BCrypt/Argon2id)
- `security_stamp` (VARCHAR, NOT NULL) - 密碼異動或登出時更新，用於即時註銷歷史 Session Cookie
- `failed_login_attempts` (INT, Default 0) - 連續登入失敗計數
- `lockout_end_at` (TIMESTAMPTZ, Nullable) - 帳號暫時鎖定截止時間（連續 5 次錯誤鎖定 15 分鐘）
- `display_name` (VARCHAR)
- `status` (SMALLINT: 0=Pending, 1=Active, 2=Suspended)
- `email_verified_at` (TIMESTAMPTZ, Nullable)
- `created_at` / `updated_at` (TIMESTAMPTZ)

### 2. `verification_tokens` (驗證碼與安全憑據表)
- `id` (UUID, PK)
- `member_id` (UUID, FK -> members.id)
- `type` (SMALLINT: 1=EmailVerification, 2=PasswordReset)
- `token_hash` (VARCHAR, Indexed)
- `expires_at` (TIMESTAMPTZ)
- `used_at` (TIMESTAMPTZ, Nullable)
- `created_at` (TIMESTAMPTZ)

### 3. `outbox_messages` (信件發送交易任務表 - Transactional Outbox)
- `id` (UUID, PK)
- `event_type` (VARCHAR: "EmailVerificationRequested", "PasswordResetRequested")
- `payload` (JSONB) - 存放收件人、Token、信件樣板參數
- `status` (SMALLINT: 0=Pending, 1=Sent, 2=Failed)
- `retry_count` (INT, Default 0)
- `created_at` (TIMESTAMPTZ)
- `processed_at` (TIMESTAMPTZ, Nullable)

---

## 五、前端架構與開發規範 (遵循 vue-best-practices)

專案前端（`apps/member-web`）強制套用 **`vue-best-practices`** 技能規範，嚴格遵守以下開發準則：

1. **核心架構與標準**：
   - 全面採用 **Composition API** 與 **`<script setup lang="ts">`**。
   - SFC 結構嚴格遵循順序：**`<script>` → `<template>` → `<style>`**。
2. **組件職責切分 (Keep Components Focused)**：
   - **薄路由視圖 (Thin Views)**：`src/views/` 下的頁面組件僅作為排版容器與功能組裝層（Composition Surface），不堆砌大量業務標記與狀態邏輯。
   - **功能模組組件化**：表單、按鈕、驗證碼輸入器等均抽取為獨立聚焦的 Child Component（放置於 `src/components/auth/`、`src/components/profile/` 等）。
3. **邏輯與狀態抽離 (Composables)**：
   - 所有非純 UI 的狀態與副作用一律封裝至 Composables：
     - `useAuth()`：登入、登出、身分狀態。
     - `usePasswordReset()`：忘記密碼與重設密碼流程。
     - `useVerification()`：Email 啟用與驗證碼處理。
4. **明確資料流與響應性設計**：
   - 遵從 **Props Down, Events Up**，明確透過 `defineProps<T>()` 與 `defineEmits<T>()` 定義型別合約。
   - 保持最小原始狀態（Minimal Source State：`ref` / `reactive`），盡可能透過 `computed` 衍生狀態，避免在模板中進行繁複計算。

---

---

## 六、執行階段與進度追蹤 (Milestones)

> **開發準則**：本專案全程採用 BDD（行為驅動開發）方法，API 測試統一採用 **BDD + WebApplicationFactory + Testcontainers**。**測試案例撰寫完成後，必須先讓使用者檢視並確認審核，方可開始實作**。

- [ ] **Phase 1: 規格先行 (API First)**
  - [ ] 1.1 撰寫 OpenAPI 3.0 YAML 規格檔案 (`docs/specs/member-api-v1.yaml`)
  - [ ] 1.2 定義註冊、登入之 Request/Response DTO 與 Schema
  - [ ] 1.3 定義忘記密碼、重設密碼、變更密碼之 Request/Response DTO 與 Schema
  - [ ] 1.4 定義 RFC 7807 錯誤格式規範（Validation & Business Error Details）
  - [ ] 1.5 完成 OpenAPI 規格驗證與審查

- [ ] **Phase 2: 後端基礎設施搭建 (ASP.NET Core)**
  - [ ] 2.1 建立 `apps/member-api` 解決方案與 Clean Architecture 專案結構
  - [ ] 2.2 配置 PostgreSQL 連線、EF Core DbContext 與 Migration 機制
  - [ ] 2.3 配置 HttpOnly Cookie 認證中介層（支援 Domain: `.1111.com.tw`）
  - [ ] 2.4 配置全域例外處理中介軟體（RFC 7807 ProblemDetails）
  - [ ] 2.5 配置 Transactional Outbox 背景發信 Worker (`EmailDispatchWorker`)
  - [ ] 2.6 設置本機開發用 `docker-compose.yml`（含 PostgreSQL 容器）

- [ ] **Phase 3: 後端業務邏輯與測試實作**
  - [ ] 3.1 實作會員資料存取層 (Member Repository & VerificationToken Repository)
  - [ ] 3.2 實作安全密碼雜湊模組 (Password Hasher / Argon2id 或 BCrypt)
  - [ ] 3.3 實作身分認證服務 (Register, Login, Logout, VerifyEmail)
  - [ ] 3.4 實作登入失敗計數與防爆破帳號暫時鎖定邏輯 (Account Lockout：5 次錯誤鎖定 15 分鐘)
  - [ ] 3.5 實作忘記密碼服務 (ForgotPassword：產生驗證 Token 並寫入 Outbox 交易)
  - [ ] 3.6 實作重設密碼服務 (ResetPassword：驗證 Token 有效性並更新密碼，刷新 Security Stamp)
  - [ ] 3.7 實作變更密碼服務 (ChangePassword：驗證舊密碼並更新密碼，刷新 Security Stamp)
  - [ ] 3.8 實作 Controller 端點並套用資料驗證 (FluentValidation)
  - [ ] 3.9 編寫 BDD 驗收情境並提交使用者審查確認
  - [ ] 3.10 實作 BDD + WebApplicationFactory + Testcontainers 整合測試（PostgreSQL 容器）

- [ ] **Phase 4: 前端介面開發 (Vue 3 遵循 vue-best-practices)**
  - [ ] 4.1 初始化 `apps/member-web` (Vite + Vue 3 + TypeScript + Tailwind CSS + shadcn-vue)
  - [ ] 4.2 配置 Vue Router（薄路由視圖、Auth Guard）與 Pinia 身分狀態管理
  - [ ] 4.3 依據 OpenAPI 規格生成 TypeScript API 客戶端與型別合約
  - [ ] 4.4 實作共用業務 Composables (`useAuth`, `usePasswordReset`, `useVerification`)
  - [ ] 4.5 實作「註冊」與「Email 驗證」頁面（含表單子組件與驗證邏輯）
  - [ ] 4.6 實作「登入」頁面（支援 `returnUrl` 轉址引導）與「登出」功能
  - [ ] 4.7 實作「忘記密碼」頁面（輸入 Email 申請重設信）
  - [ ] 4.8 實作「重設密碼」頁面（攜帶 Token 輸入並確認新密碼）
  - [ ] 4.9 實作「會員中心個人資料」與「變更密碼」頁面
  - [ ] 4.10 執行 `vue-best-practices` 自檢清單（SFC 結構、Reactivity 衍生狀態、組件職責邊界）

- [ ] **Phase 5: 整合聯調與驗收**
  - [ ] 5.1 本機前後端聯調測試（驗證 HttpOnly Cookie 跨域攜帶與寫入）
  - [ ] 5.2 測試登入成功後的 `returnUrl` 安全轉址跳轉
  - [ ] 5.3 驗證錯誤處理流程（密碼錯誤、Token 過期、重複註冊等）
  - [ ] 5.4 計畫驗收完成並封存（移至 `.archive/`）
