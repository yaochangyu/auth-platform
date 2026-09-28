# 統一身分與授權平台 (auth-platform)

統一身分與授權平台（`auth-platform`）負責管理平台自然人之會員身分識別、認證憑據、會話狀態以及第三方應用程式授權。

---

## 專案概覽 (Project Overview)

本專案採用 Monorepo 架構，主要包含以下核心模組：

- **`apps/member-api`**：基於 .NET 10 (LTS) 與 ASP.NET Core 建置之會員中心後端 API，採用 Clean Architecture 分層架構、PostgreSQL EF Core 資料持久化以及發信交易任務模式 (Transactional Outbox)。
- **`apps/member-web`**：基於 Vue 3 + Vite + TypeScript + Tailwind CSS 建置之會員中心前台介面。
- **`tests/MemberApi.Tests`**：基於 Reqnroll BDD、`WebApplicationFactory` 與 Testcontainers (PostgreSQL) 的端到端整合測試套件。

---

## 先決條件 (Prerequisites)

在開始本機開發前，請確保已安裝以下工具與執行環境：

- **.NET 10 SDK** (LTS)
- **Node.js 20+** 與 **npm**
- **Docker**（本機執行 Docker 守護進程，供 BDD Testcontainers 整合測試自動拉取並啟動 PostgreSQL 容器）

---

## 本機資料庫與設定 (Database & Configuration)

後端支援透過 `dotnet user-secrets` 安全管理敏感設定，避免將本機資料庫密碼簽入版本控制：

1. **設定本機 PostgreSQL 連線字串**：
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=member_center;Username=postgres;Password=your_password" --project apps/member-api
   ```

2. **套用資料庫遷移 (EF Core Migration)**：
   ```bash
   dotnet ef database update --project apps/member-api
   ```

---

## 啟動與測試指令 (Running & Testing)

### 1. 啟動後端 API
```bash
dotnet run --project apps/member-api
```
- API 預設監聽位址：`http://localhost:5138` (HTTP) 與 `https://localhost:7031` (HTTPS)

### 2. 啟動前端 Web
```bash
cd apps/member-web
npm install
npm run dev
```
- 前端預設開發位址：`http://localhost:5173`

### 3. 執行自動化測試
```bash
dotnet test AuthPlatform.slnx
```
> **注意**：整合測試採用單一最高測試接縫（Single High Seam Policy），執行時會由 Testcontainers 自動啟動隔離的 PostgreSQL 測試容器，驗證完畢後自動銷毀回收，無需手動維護測試資料庫。

---

## API 契約與文件 (API Contracts & Documentation)

- **OpenAPI 3.0 規格文件**：位於 [`docs/specs/member-api-v1.yaml`](docs/specs/member-api-v1.yaml)，全面遵循統一領域語言（Ubiquitous Language）與 RFC 7807 (`application/problem+json`) 錯誤回應規範。
- **本機 OpenAPI / Swagger 端點**：
  - OpenAPI 規格端點：`http://localhost:5138/openapi/v1.json`
  - Swagger UI 文件介面：`http://localhost:5138/swagger`
