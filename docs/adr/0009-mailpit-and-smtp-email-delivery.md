# ADR 0009：Mailpit 虛擬郵件伺服器整合與標準 SMTP 發信機制

## 狀態
已拍板 (Accepted) - 2026-09-30

## 背景
平台在會員註冊認證、電子郵件啟用、忘記密碼與密碼重設等核心流程中，均依賴電子郵件投遞包含時效性驗證 Token 的連結。
在過往實作中，發信端採用 `LoggingEmailSender` 僅於控制台輸出日誌以供本機觀察，存在以下問題：
1. **通訊協定脫節**：未真正透過 SMTP 網路協議建立連線、握手與 MIME 封裝，無法在開發與測試環境提早發現郵件格式、編碼或傳輸層異常。
2. **測試信件外流風險**：若在測試環境直接串接正式外部 SMTP 伺服器，可能誤將測試連結與機敏 Token 發送至外部真實信箱，造成資訊外流、干擾使用者並消耗郵件額度。
3. **自動化測試脆弱**：煙霧測試依賴從容器控制台日誌刮取字串，易受日誌輸出延遲、緩衝或格式變更影響，缺乏穩定確定性。

## 決策內容 (Decisions)

### 1. 容器編排環境引入 Mailpit 虛擬郵件伺服器
- 在本機與整合驗收環境（`docker-compose.yml`）中引入輕量級虛擬郵件伺服器 `axllent/mailpit`。
- **連接埠配置**：
  - SMTP 服務監聽埠 `1025`：接收所有外發郵件，全數攔截暫存於記憶體，絕對不向外部網路投遞。
  - Web UI 與 REST API 監聽埠 `8025`：供開發者透過瀏覽器檢視信件視覺排版與 HTML 原始碼，並提供自動化測試腳本進行信件檢索與 Token 萃取。

### 2. 基於 MailKit 實作標準 SMTP 發信服務
- 會員 API（`member-api`）引入微軟官方推薦之 .NET 郵件傳輸函式庫 `MailKit`。
- 實作 `SmtpEmailSender` 支援標準 SMTP / STARTTLS 協定，讀取 `SmtpOptions` 組態（Host、Port、Sender、Credentials、SecureSocketOptions）。
- 正式環境與測試環境共享相同的 SMTP 業務程式碼，僅需透過環境變數或配置檔切換連線端點，達成零供應商鎖定。

### 3. 多環境切換機制與回退支援
- 透過組態 `Email:Provider`（`Smtp` 或 `Logging`）支援彈性切換：
  - **Docker Compose / 開發環境**：預設採用 `Smtp`，指向容器內部網路之 `mailpit:1025`。
  - **單元與 BDD 整合測試（WebApplicationFactory）**：預設採用 `Logging` 或測試用注入，確保測試環境無需額外外部 SMTP 依賴，執行迅速且無 Flaky 風險。
  - **正式環境**：設定 `Smtp` 並配置真實外部郵件伺服器憑證。

### 4. 自動化測試腳本升級為 REST API 驅動
- 建立共用郵件解析函式庫 `scripts/lib-mail.sh`。
- 煙霧測試腳本（`smoke-test.sh`、`oauth-smoke-test.sh`、`phase3-smoke-test.sh`）優先呼叫 Mailpit REST API（`/api/v1/search` 與 `/api/v1/message/{id}`）精確檢索目標信件並解析驗證 Token。
- 保留容器控制台日誌解析作為無 Mailpit 狀態下的降級回退機制。

## 後果 (Consequences)

- **正面效益**：
  1. 本機開發與 Docker 驗收能夠走完真實 SMTP 協議傳輸，同時杜絕測試信件外流風險。
  2. 開發者可直接開啟 `http://localhost:8025` 視覺化檢閱郵件內容與連結。
  3. 自動化測試告別脆弱的日誌爬取，提升端到端測試的穩定度與執行速度。
- **承擔代價**：
  1. Docker Compose 需多維護一個 Mailpit 容器服務（佔用極少記憶體約 15MB）。
