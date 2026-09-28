# 0003. 採用發信交易任務模式 (Transactional Outbox) 保證郵件派發可靠性

- **狀態 (Status)**: 已採納 (accepted)
- **日期 (Date)**: 2026-09-28

## 背景脈絡 (Context)

會員註冊與忘記密碼等流程需要發送驗證權杖信件。若在 HTTP 請求處理管線中同步直接呼叫外部寄信服務（如 SMTP、SES 或 SendGrid），會引入不可預期的網路延遲，且面臨雙寫失敗問題（例如：資料庫寫入成功但發信超時報錯，或者信件已寄出但資料庫交易復原）。

## 決策 (Decision)

我們決定採用 **發信交易任務模式（Transactional Outbox Pattern）**。
每當會員註冊或請求密碼重設權杖時，系統在同一個 PostgreSQL 資料庫 ACID 交易中，同時寫入會員/權杖實體變更與一筆 `outbox_messages` 紀錄。由背景託管工作服務（`EmailDispatchWorker`）定期輪詢或事件喚醒，非同步執行信件寄送，並具備指數退避重試機制，成功後將訊息標記為已處理。

## 後果與權衡 (Consequences)

- 即使外部郵件供應商暫時斷線或應用程式重啟，亦能保證至少派發一次 (At-least-once) 的可靠性。
- 將外部網路延遲完全從面向使用者的 API 回應時間中剝離，大幅提升 API 吞吐量與反應速度。
- 需在 `apps/member-api` 維護額外的 `outbox_messages` 表與背景背景處理 Worker。
