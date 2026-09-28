# 0001. API 測試策略：採用 BDD、WebApplicationFactory 與 Testcontainers

- **狀態 (Status)**: 已採納 (accepted)
- **日期 (Date)**: 2026-09-28

## 背景脈絡 (Context)

後端 API 的測試常陷入對倉儲 (Repository)、資料庫 Context 與 HTTP 層進行過度 Mock 的陷阱。這類隔離式單元測試非常脆弱，在重構內部實作時往往需要大量修改，且無法驗證真實 SQL / PostgreSQL 約束或實際的 Cookie 與認證中介層行為。

## 決策 (Decision)

我們決定在 API 測試中全面且唯一採用 **BDD（行為驅動開發）+ WebApplicationFactory + Testcontainers (PostgreSQL)**。所有測試情境皆以使用者視角的 Given-When-Then 規格編寫，針對 Docker 容器中啟動的真實資料庫進行端到端黑箱/灰箱驗證，跳過隔離的 Mock 單元測試。測試案例撰寫完成後，必須先讓使用者檢視並確認審核，方可開始實作。

## 後果與權衡 (Consequences)

- 高信心度：保證認證 Cookie、資料庫 Migration 與 SQL 查詢在真實 PostgreSQL 引擎下正確運行。
- 重構友善：只要對外 API 契約與行為不變，重構內部服務或資料存取層不會導致測試壞掉。
- 測試執行環境需具備 Docker 運行能力。
