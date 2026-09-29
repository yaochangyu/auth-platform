# 0007. OAuth 管理平台架構劃分、Dogfooding 鑑權與 Server-to-Server 安全整合

- **狀態 (Status)**: 已採納 (accepted)
- **日期 (Date)**: 2026-09-29

## 背景脈絡 (Context)

在建構統一身分與授權平台時，除了高吞吐的公開授權伺服器（`auth-server`）外，系統亟需一個供內部維運人員（Admin）與各產品線開發人員（Developer）自主管理 Client 應用程式、授權範疇、金鑰輪替與稽核的**管理平台**。
同時，系統需滿足無人介入的「伺服器對伺服器（Server-to-Server, M2M）」呼叫場景。在現代雲端動態出口 IP（無固定 IP）的限制下，需提供超越單純靜態 IP 白名單的高強度資安防護機制。

## 決策 (Decision)

### 1. 管理平台實體服務劃分與架構解耦
- **獨立服務模式**：
  - 後端：獨立建立 **`apps/auth-admin-api`**（ASP.NET Core Web API），提供管理 RESTful 端點。
  - 前端：獨立建立 **`apps/auth-admin-web`**（Vue 3 + Vite + Tailwind + shadcn-vue），提供管理控制台 SPA。
- **解耦效益**：將後台管理操作、批次報表與複雜權限查詢，與對外的公開授權發號核心（`auth-server`）在進程與流量上完全實體隔離，確保高併發授權端點的純粹性與穩定性。

### 2. Dogfooding 鑑權與專案擁有權隔離 (Application Ownership)
- **吃自己的狗糧 (Dogfooding)**：管理平台前端登入直接作為自身 `auth-server` 的標準 First-party OAuth Client。管理者與開發者透過統一會員帳密登入，`auth-server` 簽發帶有角色（`role: "admin"` 或 `role: "developer"`）的 JWT。
- **數據所有權隔離**：
  - 開發者建立 Client 或 API Key 時自動綁定其 `MemberId`，僅能檢視與維護自身擁有的應用程式與憑證。
  - 系統管理員擁有全域權限（God View），可審核、停用任一 Client，並查看全平台不可篡改的 `Audit Log`。

### 3. Client Secret 安全生命週期
- **單次展示**：Secret 產生當下在前端僅明文展示一次，關閉後無法再檢視。
- **單向雜湊存儲**：資料庫僅保存單向雜湊值（SHA-256），後端完全無法反解。
- **平滑過渡輪替 (Graceful Rotation)**：支援雙金鑰並存過渡期（如 24 小時），待正式服務更新完成後再作廢舊密鑰，確保線上維運零斷線。

### 4. 統一整合 Server-to-Server (M2M) 鑑權機制
管理平台同時納管對人的「OAuth Client」與對機器的「M2M 鑑權」，針對雲端無固定 IP 環境提供雙重防護：
- **方案 A：OAuth 2.1 Client Credentials Grant**：
  後端服務以 `client_id` + `client_secret` 換取短效（15 分鐘）Access Token，降低長期憑證暴露風險。
- **方案 B：API Key + HMAC 請求簽章（防重放與防竄改）**：
  - API Key 格式遵循 Stripe / GitHub 現代前綴規範（`ak_live_...` / `ak_test_...`），單向雜湊存儲。
  - **HMAC Request Signature**：客戶端以金鑰對 HTTP Method、Path、Timestamp 與 Body 雜湊計算 HMAC-SHA256，並攜帶 `X-Signature` 與 `X-Timestamp`。
  - 伺服器端比對時間戳記（公差 ±5 分鐘防止重放攻擊）與 Body 簽章（防止中途竄改），在不依賴固定 IP 的前提下達成最高安全防禦。

## 後果與權衡 (Consequences)

- **優點**：
  - 平台職責極度清晰，管理流量不搶佔授權核心頻寬。
  - 徹底解決動態雲端環境無法配置 IP 白名單的痛點，以標準 HMAC 簽章與 Client Credentials 提供高安全性。
  - 自助式開發者入口釋放維運人力，同時保留管理員最高斷路權。
- **權衡與代價**：
  - 新增 `apps/auth-admin-api` 與 `apps/auth-admin-web` 兩座工程目錄，需在 Docker Compose 中追加編排。
  - HMAC 簽章驗證在呼叫端需要輔助 SDK 或標準封裝以計算簽章。
