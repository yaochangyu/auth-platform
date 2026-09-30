# Mailpit 虛擬郵件伺服器整合與標準 SMTP 發信防護機制規格

- **GitHub Issue**: [#29](https://github.com/yaochangyu/auth-platform/issues/29)
- **狀態**: `ready-for-agent` (已驗收結案)

## Problem Statement

目前系統的會員註冊驗證與忘記密碼等流程需發送 Email 驗證連結。在測試與本機開發階段，若直接連線真實郵件伺服器，會將測試信件與驗證連結發送至外部真實信箱，造成資訊外流、干擾真實使用者信箱以及消耗真實郵件配額；反之，若僅以日誌輸出模擬（Logging），則無法驗證真實 SMTP 網路通訊協定與 MIME 封裝，導致測試環境與正式環境的發信行為脫節。

## Solution

引入支援標準 SMTP 協定的發信機制與容器化虛擬郵件伺服器（Mailpit）攔截架構：
1. 應用層實作標準 SMTP 發信服務（基於 .NET 推薦之 MailKit 函式庫），並透過配置支援 `Email:Provider`（`Smtp` 與 `Logging`）切換。
2. 容器編排環境引入輕量級 Mailpit 服務，本機開發與 Docker 煙霧測試統一將 SMTP 指向 Mailpit（Port 1025）。Mailpit 攔截所有外發信件、不連外網，並提供 Web UI（Port 8025）供開發者檢視，以及 REST API 供自動化測試腳本檢索信件內容與驗證 Token。
3. 正式環境僅需調整組態指向真實外部 SMTP 伺服器，不需修改任何發信業務邏輯程式碼。

## User Stories

1. As a platform developer, I want all outgoing emails during local development and testing to be intercepted by a virtual SMTP server (Mailpit), so that no test verification emails or password reset links are accidentally delivered to real-world recipient inboxes.
2. As a platform developer, I want to view intercepted emails and their rendered HTML/text contents in a local Web UI, so that I can easily verify email formatting, templates, and link correctness without checking log files.
3. As a platform developer, I want the email sending component to communicate via the standard SMTP protocol using MailKit, so that local development and production environments run identical email transport code.
4. As a test automation engineer, I want an automated test script to retrieve verification tokens and password reset links via Mailpit's REST API, so that end-to-end integration tests do not rely on fragile console log scraping.
5. As a DevOps engineer, I want to configure the email delivery provider via configuration (`Email:Provider`), so that the service can seamlessly switch between SMTP delivery and console logging fallback without rebuilding the application.
6. As a DevOps engineer, I want SMTP connection parameters (host, port, credentials, TLS options, sender address, and sender display name) to be configurable via environment variables or configuration files, so that production secrets and endpoints remain completely decoupled from the codebase.
7. As a security officer, I want sensitive account verification links and tokens to remain strictly within the isolated internal network during testing, so that credentials and reset tokens are never exposed over public mail networks.
8. As a test automation engineer, I want the end-to-end smoke test scripts to verify the complete SMTP delivery lifecycle (from API trigger, across network transport to SMTP server, to REST retrieval), so that any protocol-level network failures are caught before production deployment.
9. As a platform developer, I want clear architectural decision records (ADR) detailing the Mailpit integration and SMTP provider design, so that team members understand the design rationale and testing workflow.
10. As a platform developer, I want existing unit and integration test suites (`WebApplicationFactory`) to continue executing reliably and deterministically, so that email testing does not introduce flaky dependencies or network delays in CI.

## Implementation Decisions

- 應用程式發信模組改進：
  - 引入成熟且微軟官方推薦的 SMTP 用戶端函式庫（MailKit），實作標準 SMTP 發信器。
  - 保留現有控制台日誌發信器作為備援回退機制。
  - 統一實作既有之電子郵件發信介面，確保會員註冊、忘記密碼等業務處理器無感切換。
- 組態模型與多環境切換：
  - 定義標準 SMTP 組態規格，包含伺服器主機名、傳輸埠號、發件人信箱、發件人顯示名稱、認證帳號與密碼，以及安全傳輸選項（None / StartTls / SslOnConnect）。
  - 提供發信提供者選擇器（`Email:Provider`），支援 `Smtp` 與 `Logging` 模式。
- 基礎架構與容器編排：
  - 在本機容器編排環境（Docker Compose）中新增虛擬郵件伺服器服務（Mailpit）。
  - 將 SMTP 服務端點暴露於內部網路 Port 1025，並將 Web 管理面板與 REST API 暴露於 Port 8025。
  - 預設將 API 容器的 SMTP 組態指向容器內部網路之 Mailpit 服務。
- 自動化驗收與腳本升級：
  - 升級既有的自動化煙霧測試腳本，新增共用函式庫以優先透過 Mailpit REST API 查詢目標信箱的最新信件，並自信件主體提取驗證碼與密碼重設 Token。
  - 保留容器日誌正則表達式解析作為回退降級模式，確保在無外部郵件服務環境下的相容性。

## Testing Decisions

- 測試原則：只測試對外公開行為與通訊協定完整性，不測試函式庫內部實作細節。
- 測試接縫（Seams）：
  1. **最高端到端接縫（Highest Seam - E2E Smoke Test）**：
     - 透過真實運行的 Docker Compose 容器群，由測試腳本發送真實 HTTP 請求至會員 API 觸發發信（如註冊、忘記密碼），隨後呼叫 Mailpit REST API（`http://localhost:8025/api/v1/messages`）檢索真實投遞之信件與 Token。此接縫貫穿 HTTP API -> 業務 Handler -> SMTP 網路通訊 -> 郵件伺服器持久化 -> REST API 檢索全鏈路。
  2. **整合測試接縫（Integration Seam - WebApplicationFactory）**：
     - 在 C# 自動化測試專案中，透過 `WebApplicationFactory<Program>` 注入或使用測試環境組態，驗證端點調用時發信元件被正確調度，且組態切換正常運作。
- 既有先例（Prior Art）：
  - 參考現有 `scripts/smoke-test.sh`、`scripts/oauth-smoke-test.sh` 與 `tests/MemberApi.Tests` 的整合測試設計模式。

## Out of Scope

- 自建正式環境的實體或雲端 SMTP 伺服器集群（正式環境採用外部已有基礎設施或雲端服務如 SES/SendGrid）。
- 實作特定的第三方 Proprietary HTTP SDK（如 AWS SES SDK、SendGrid Web API），維持使用標準 SMTP 協定以達到零供應商鎖定。
- 客製化複雜的視覺化 Email 樣板引擎（如 MJML、Razor 郵件範本編譯；此規格維持現有文字與連結排版）。
- 收件伺服器（POP3 / IMAP）實作。

## Further Notes

- 遵循專案既有 ADR 規範，實作完成時將補齊相應的架構決策紀錄（ADR 0009）。
- 所有變更維持向下相容，既有 BDD 測試套件（278 項情境）維持 100% 通過。
