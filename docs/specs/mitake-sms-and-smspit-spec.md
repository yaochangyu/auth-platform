# 三竹簡訊 (Mitake SMS) 整合與容器化虛擬 SMS 攔截防護機制規格

- **GitHub Issue**: [#32](https://github.com/yaochangyu/auth-platform/issues/32)
- **狀態**: `ready-for-agent`

## Problem Statement

平台正進行會員手機號碼綁定與簡訊 OTP 驗證機制規劃。在正式環境中將串接台灣三竹簡訊（Mitake SMS）電信閘道發送 6 碼驗證碼；但在本機開發、測試與 CI/CD 自動化環境中，若直接調用真實簡訊 API，將產生實體電信費用、誤發簡訊給真實門號、以及面臨機敏驗證碼外洩之風險。若僅以日誌輸出（Logging）模擬，則無法驗證 HTTP 協定請求組裝、表單編碼、回應代碼解析與逾時處理，且自動化測試難以高可靠地擷取 OTP。

## Solution

引入比照 Mailpit 模式的三竹簡訊整合與容器化虛擬 SMS 伺服器（Smspit）攔截防護機制：
1. 應用層定義統一的簡訊發送介面 `ISmsSender`，實作標準三竹簡訊發送器（`MitakeSmsSender`），支援帳號、密碼、端點與逾時等參數配置化，並以 `Sms:Provider`（`Mitake` 與 `Logging`）支援環境切換。
2. 在 Docker Compose 引入輕量級虛擬 SMS 服務（Smspit，Port 8026），完整模擬三竹簡訊 HTTP API 端點，攔截所有簡訊不連外網、不產生任何費用，並提供即時 Web UI 供開發者檢視簡訊排版，以及 REST API 供自動化測試腳本檢索最新 6 碼 OTP。
3. 測試環境與正式環境運行完全相同的發信與 HTTP 請求程式碼，僅切換目標端點 URL，達成環境一致性與零電信成本。

## User Stories

1. As a platform developer, I want all outgoing SMS messages during local development and testing to be intercepted by a virtual SMS server (Smspit), so that no real SMS messages are sent to actual mobile phones and zero telecom fees are incurred.
2. As a platform developer, I want to view intercepted SMS messages, recipient phone numbers, and timestamps in a local Web UI, so that I can easily verify SMS template correctness and OTP readability.
3. As a platform developer, I want the SMS sender component to communicate using Mitake SMS Gateway's standard HTTP API protocol, so that local development and production environments run identical HTTP client transmission and response parsing code.
4. As a test automation engineer, I want an automated test script to retrieve 6-digit SMS OTP verification codes via Smspit's REST API, so that end-to-end integration tests can deterministically verify phone verification flows without fragile log scraping.
5. As a DevOps engineer, I want to configure the SMS delivery provider via configuration (`Sms:Provider`), so that the service can seamlessly switch between Mitake SMS Gateway and console logging fallback without rebuilding the application.
6. As a DevOps engineer, I want Mitake connection parameters (endpoint URL, username, password, client ID) to be configurable via environment variables or configuration files, so that production credentials and endpoints remain completely decoupled from the codebase.
7. As a security officer, I want sensitive OTP codes and phone numbers to remain strictly within the isolated internal network during testing, so that user credentials are never exposed over public telecommunication networks.
8. As a test automation engineer, I want end-to-end smoke test scripts to verify the complete SMS delivery lifecycle (from API trigger, across network transport to mock SMS server, to REST retrieval), so that any protocol-level network failures are caught before production deployment.
9. As a platform developer, I want clear architectural decision records (ADR) detailing the Mitake SMS integration and virtual SMS mock server design, so that team members understand the design rationale and testing workflow.
10. As a platform developer, I want existing unit and integration test suites (`WebApplicationFactory`) to continue executing reliably and deterministically, so that SMS testing does not introduce flaky external dependencies or network delays in CI.

## Implementation Decisions

- 應用程式簡訊模組設計：
  - 定義 `ISmsSender` 抽象介面，提供標準發送協定。
  - 實作三竹簡訊用戶端（`MitakeSmsSender`），封裝三竹 HTTP POST 傳輸協定，處理受訊門號（`dstaddr`）、簡訊內容（`smbody`）、帳號驗證與回應代碼解析。
  - 保留控制台日誌發送器（`LoggingSmsSender`）作為備援回退機制。
- 組態模型與多環境切換：
  - 定義三竹簡訊組態規格（`MitakeSmsOptions`），包含 API 端點 URL、帳號、密碼、預設發送設定與逾時秒數。
  - 提供發送提供者選擇器（`Sms:Provider`），支援 `Mitake` 與 `Logging` 模式。
- 基礎架構與容器編排：
  - 在本機容器編排環境（Docker Compose）中新增虛擬 SMS 伺服器服務（`smspit`，Port 8026）。
  - 提供三竹相容接收端點（`POST /api/mtk/SmSend`），模擬三竹標準成功回應格式（包含 `statuscode=1` 與虛擬 `msgid`）。
  - 提供 REST API（`GET /api/v1/messages`）與輕量 Web 面板，供查詢簡訊紀錄與擷取 OTP。
  - 預設將 API 容器的 SMS 組態指向容器內部網路之 `http://smspit:8026/api/mtk/SmSend`。
- 自動化驗收與腳本升級：
  - 擴充共用通訊輔助庫，優先透過 Smspit REST API 查詢目標手機號碼的最新簡訊並萃取 6 碼數字 OTP。
  - 保留容器日誌正則表達式解析作為回退降級模式。

## Testing Decisions

- 測試原則：只測試對外公開行為與通訊協定完整性，不測試函式庫內部實作細節。
- 測試接縫（Seams）：
  1. **最高端到端接縫（Highest Seam - E2E Smoke Test）**：
     - 透過真實運行的 Docker Compose 容器群，測試腳本發送真實 HTTP 請求至會員 API 觸發發送簡訊（如發送 OTP），隨後直接呼叫 Smspit REST API（`http://localhost:8026/api/v1/messages?to=0912345678`）檢索真實透過 HTTP 投遞之簡訊與 OTP。此接縫貫穿 HTTP API -> 業務 Handler -> HTTP Client 網路通訊 -> 虛擬簡訊伺服器持久化 -> REST API 檢索全鏈路。
  2. **整合測試接縫（Integration Seam - WebApplicationFactory）**：
     - 在 C# 自動化測試專案中，透過 `WebApplicationFactory<Program>` 注入或使用測試環境組態，驗證端點調用時簡訊發送元件被正確調度，且組態切換正常運作。
- 既有先例（Prior Art）：
  - 參考 Issue #29 / #30 / #31 的 Mailpit 與 SmtpEmailSender 整合測試設計模式。

## Out of Scope

- 會員資料庫 Schema 新增 `phone_number` 欄位與 EF Core Migration（此規格聚焦於 SMS 發送基礎設施與三竹協議模擬，手機註冊與雙軌登入邏輯保留於 Issue #21）。
- 實作三竹簡訊的狀態回報接收端點（DLR / Delivery Report Webhook），初版僅聚焦於發送與驗證碼即時萃取。
- 語音驗證碼（Voice OTP）或 MMS 圖片簡訊。

## Further Notes

- 遵循專案既有 ADR 規範，實作完成時將補齊相應的架構決策紀錄（ADR 0010）。
- 所有變更維持向下相容，既有全套測試維持 100% 通過。
