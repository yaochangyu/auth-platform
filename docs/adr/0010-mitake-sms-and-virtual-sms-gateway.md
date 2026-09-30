# ADR 0010：三竹簡訊 (Mitake SMS) 整合與容器化虛擬 SMS 攔截防護機制

## 狀態
已拍板 (Accepted) - 2026-09-30

## 背景
平台正進行會員手機號碼綁定與簡訊 OTP 驗證機制規劃（對齊 Issue #21）。
在正式環境中將串接台灣三竹簡訊（Mitake SMS）電信閘道發送 6 碼驗證碼；但在本機開發、測試與 CI/CD 自動化環境中，直接調用外部真實簡訊服務存在以下問題：
1. **電信成本與配額消耗**：頻繁自動化測試會累積可觀的簡訊發送通訊費用。
2. **干擾真實用戶與機敏外洩**：若測試資料使用真實或非特定測試門號，可能造成一般用戶收到不明 OTP，或將驗證碼曝露於公共電信通道。
3. **通訊協定行為脫節**：若僅以控制台日誌（Logging）模擬，無法提早發現 HTTP 表單編碼、網路逾時、狀態碼解析等傳輸層異常。
4. **自動化測試脆弱**：煙霧測試若依賴日誌爬取易受輸出延遲影響，缺乏高穩定度與確定性。

## 決策內容 (Decisions)

### 1. 容器編排環境引入 Smspit 虛擬簡訊伺服器
- 在本機與整合驗收環境（`docker-compose.yml`）中引入極輕量虛擬簡訊服務 `smspit`（監聽連接埠 `8026`）。
- **服務能力**：
  - **三竹相容發送端點**：`POST /api/mtk/SmSend`，接收三竹標準之 `dstaddr`（手機門號）、`smbody`（簡訊內文）、`username` 與 `password`，將簡訊暫存於記憶體，並回傳三竹標準之 `[#001]\nmsgid=...\nstatuscode=1\n` 回應。
  - **REST API 端點**：`GET /api/v1/messages?to={phone}`，供測試腳本查詢發往特定號碼之簡訊與擷取 6 碼數字 OTP。
  - **即時 Web 管理面板**：`GET /`，供開發者開啟瀏覽器檢視簡訊排版、門號與時間戳記。

### 2. 應用層實作 MitakeSmsSender
- 會員 API（`apps/member-api`）定義 `ISmsSender` 抽象介面。
- 實作以 `HttpClient` 為基礎的 `MitakeSmsSender`，嚴格遵守三竹表單編碼與正規表達式狀態碼解析（`statuscode=1` 或 `0` 判定為成功）。
- 於 `Program.cs` 以 `Transient` 註冊，並配置 `HttpClient` 預設逾時（10 秒），避免 Captive Dependency。

### 3. 多環境切換機制與回退支援
- 透過組態 `Sms:Provider`（`Mitake` 或 `Logging`）支援多環境切換：
  - **Docker Compose / 開發環境**：設定 `Mitake`，`EndpointUrl` 指向容器內部網路之 `http://smspit:8026/api/mtk/SmSend`。
  - **單元與 BDD 整合測試（WebApplicationFactory）**：預設採用 `Logging`，確保測試環境無需外部網路依賴，執行迅速且無 Flaky 風險。
  - **正式環境**：設定 `Mitake` 並注入三竹正式 API 端點與真實帳號密碼。

### 4. 自動化測試腳本升級為 REST API 驅動
- 擴充共用通訊工具庫 `scripts/lib-mail.sh`，提供 `sms_otp` / `get_sms_otp` 函式。
- 煙霧測試腳本優先呼叫 Smspit REST API（`/api/v1/messages`）精確查詢目標手機門號並以正規表達式提取 6 碼數字 OTP。
- 保留容器控制台日誌解析作為無 Smspit 狀態下的降級回退機制。

## 後果 (Consequences)

- **正面效益**：
  1. 本機開發與 Docker 驗收能夠走完真實三竹 HTTP 協議傳輸，同時達成零電信成本與零簡訊外流。
  2. 開發者可直接開啟 `http://localhost:8026` 視覺化檢閱發送給特定手機門號的簡訊與驗證碼。
  3. 與 Mailpit 機制對齊，形成統一的「虛擬通訊攔截」架構體系。
- **承擔代價**：
  1. Docker Compose 增加一個極輕量的 Smspit 容器服務（佔用記憶體約 15MB）。
