# 手機號碼與簡訊 SMS OTP 驗證規格 (待實作 / Backlog)

**交付目標：**
為平台（Web 與 Native App）擴充手機號碼驗證通道。會員註冊時可填寫手機號碼並透過 6 碼簡訊 OTP 驗證；登入端點支援以「Email 或 手機號碼」雙軌識別。驗證簡訊以 Transactional Outbox Pattern 非同步可靠發送，並建立頻率限制（Rate Limiting）防範簡訊轟炸與撞庫攻擊。

**狀態：**
Backlog / Pending（架構決策已拍板，排程待實作）

## 待辦工作清單 (Checklist)

- [ ] **資料庫 Schema 擴充 (EF Core Migration)**：
  - `members.phone_number` (string, nullable, 建立唯一索引 Unique Index)
  - `members.phone_verified_at` (DateTimeOffset, nullable)
- [ ] **SMS 發送基礎設施 (Transactional Outbox)**：
  - 建立 `sms_outbox_messages` 表與專屬簡訊派發服務（`SmsDispatchWorker`）
  - 介接外部電信簡訊閘道（SMS Gateway）
- [ ] **OTP 驗證邏輯與快取防護**：
  - 實作 6 碼短效數字 OTP 產生、SHA-256 雜湊儲存
  - 5 分鐘有效期限（TTL）、最多嘗試 5 次驗證失敗即作廢
- [ ] **API 端點實作**：
  - `POST /api/v1/auth/send-sms-otp`（含 IP 與手機號碼發送頻率限制）
  - `POST /api/v1/auth/verify-phone`（校驗 OTP 並標記手機已驗證）
- [ ] **既有端點擴充**：
  - `POST /api/v1/auth/register`：支援手機號碼輸入與簡訊驗證啟用
  - `POST /api/v1/auth/login`：支援「Email 或 手機號碼」自動判斷雙軌登入
- [ ] **前端整合 (Web & App)**：
  - 整合手機號碼國際區碼/格式輸入元件
  - 60 秒倒數發送與 OTP 驗證輸入框
- [ ] **品質保證**：
  - 編寫 BDD 測試案例經使用者審查後，完成 WebApplicationFactory + Testcontainers 整合測試
