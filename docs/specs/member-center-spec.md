# 功能規格書：會員中心 (統一身分與帳號平台)

## 問題陳述 (Problem Statement)

平台使用者目前缺乏一個集中式的自助服務入口來管理其數位身分。具體而言，缺乏標準化且安全的機制讓自然人註冊帳號、驗證信箱所有權、跨多個子系統進行單一登入 (SSO)、找回遺忘的密碼憑據，或管理已授權存取個人資料的第三方應用程式。此外，後端服務缺乏具備彈性、能防範憑據暴力破解與會話劫持的安全認證邊界。

## 解決方案 (Solution)

打造一個現代化、解耦的會員中心模組，包含 ASP.NET Core 後端（`member-api`）與 Vue 3 前端（`member-web`）：
1. 提供端到端的自助式身分生命週期管理：註冊、信箱驗證、登入、登出、忘記密碼與密碼變更。
2. 採用作用域為 `.1111.com.tw` 的 HttpOnly 安全 Session Cookie，並透過安全戳記（Security Stamp）達成瞬間跨裝置會話註銷。
3. 採用發信交易任務模式 (Transactional Outbox Pattern) 保證郵件派發可靠性，消除外部發信延遲對 API 效能的影響。
4. 提供連續 5 次憑據錯誤鎖定 15 分鐘的漸進式暫時防爆破保護。
5. 提供第三方授權應用程式 (Connected Apps) 檢視與即時撤銷權限管理。

## 使用者故事 (User Stories)

1. 作為一名預備會員，我想要在註冊表單輸入 Email 與強密碼，以便在平台建立全新的身分。
2. 作為一名預備會員，我想要在註冊後收到一封 Email 驗證信，以便證明我擁有該 Email 帳號。
3. 作為一名預備會員，我想要點擊信中的啟用連結來啟用帳號，以便從「待驗證 (Pending)」狀態轉換為「已啟用 (Active)」。
4. 作為一名預備會員，當我輸入不合規的信箱格式或過弱的密碼時，我想要獲得清楚的驗證提示，以便在提交前完成修正。
5. 作為一名未驗證的使用者，當我使用同一個待驗證的 Email 再次註冊時，我希望系統靜默重寄驗證信且不覆蓋原密碼，以防範他人惡意竄改。
6. 作為一名已註冊的會員，我想要透過 Email 與密碼進行登入，以便安全存取我的個人帳號與平台服務。
7. 作為一名已註冊的會員，我希望登入會話儲存在 `.1111.com.tw` 的 HttpOnly 安全 Cookie 中，以便在平台各子網域享受單一登入 (SSO) 且免於 XSS 風險。
8. 作為一名由外部子系統導向過來的會員，我希望在登入成功後被自動導向回原始目標網址 (`returnUrl`)，以便無縫繼續原工作流。
9. 作為一名平台管理者，我希望系統阻擋不合法的開放重定向 (Open Redirect)，以防止惡意人士透過 `returnUrl` 誘導會員前往釣魚網站。
10. 作為一名尚未完成信箱驗證的會員，當我嘗試登入時系統應明確阻擋並提示先完成信箱驗證，以確保帳號真實性。
11. 作為一名已註冊的會員，當我的帳號被連續嘗試錯誤密碼達 5 次時，我希望系統自動暫時鎖定該帳號，以挫敗自動化暴力字典攻擊。
12. 作為一名被暫時鎖定的會員，我希望鎖定狀態在 15 分鐘後自動解除，以便在無須聯繫客服的情況下重新嘗試登入。
13. 作為一名已註冊的會員，我想要主動登出會員中心，以便撤銷 Session Cookie，防止共用裝置上的未授權存取。
14. 作為一名忘記密碼的會員，我想要輸入 Email 申請密碼重設信，以便重新恢復對帳號的控制權。
15. 作為一名注重隱私的會員，我希望忘記密碼請求一律回傳通用的成功訊息（不論信箱是否存在），以防止惡意人士藉此探測枚舉已註冊會員清單。
16. 作為一名申請忘記密碼的會員，我希望每次新申請時舊的未用權杖立即作廢，以確保只有最新的一封信件有效。
17. 作為一名維運人員，我希望同一個 Email 申請忘記密碼有 60 秒的冷卻間隔，以保護寄信服務不被惡意刷爆。
18. 作為一名已收到重設信的會員，我想要輸入有效權杖與符合強度的新密碼，以便安全完成密碼重設。
19. 作為一名重設密碼完成的會員，我希望該帳號在所有裝置上的歷史登入會話立即被強制失效，以確保被盜帳號徹底脫離未授權裝置。
20. 作為一名已登入的會員，我想要查看我的個人檔案資料（Email、暱稱、會員狀態），以便確認我的帳號資訊。
21. 作為一名已登入的會員，我想要在輸入正確舊密碼後變更為新密碼，以便定期維護帳號安全性。
22. 作為一名已登入的會員，當我變更密碼後，我希望其他瀏覽器上的會話立即失效，以防止外洩裝置持續登入。
23. 作為一名已登入的會員，我想要查看我曾經授權過的所有第三方「已連結應用程式」清單，以掌握個人資料存取狀況。
24. 作為一名已登入的會員，我想要主動解除/撤銷特定第三方應用程式的存取授權，以保護個人資料不再外洩。
25. 作為一名註冊或重設密碼的會員，我希望系統在外部郵件供應商暫時斷線時仍保證信件最終能寄出，使我不致於卡在驗證流程中。

## 實作決策 (Implementation Decisions)

- **架構風格**：
  - 後端採用 .NET 10 (LTS) 與 ASP.NET Core Controller 架構 + Clean Architecture 分層（Controller -> Application Service -> Repository），搭配 PostgreSQL EF Core。
  - 前端採用 Vue 3 + Composition API (`<script setup lang="ts">`) + 薄路由視圖 + 獨立 Composables (`useAuth`, `usePasswordReset`, `useVerification`) + Tailwind CSS + shadcn-vue。
  - 全專案實體命名完全對齊統一領域語言（[`CONTEXT.md`](file:///home/yao/projects/auth-platform/CONTEXT.md)）。
- **認證與會話生命週期**：
  - 使用 HttpOnly, Secure, SameSite, Domain: `.1111.com.tw` 之 Cookie Authentication。
  - 透過 `members` 表的 `security_stamp` 機制實現即時會話失效：密碼異動或重設時刷新戳記，中介層比對不符立即踢出。
- **發信可靠性與 Outbox Pattern**：
  - 產生驗證權杖與領域通知事件時，在同一個 PostgreSQL ACID 交易中寫入 `outbox_messages` 表。
  - 由非同步背景 Worker (`EmailDispatchWorker`) 讀取未發訊息並執行指數退避重試派發。
- **防爆破與帳號鎖定**：
  - 追蹤 `failed_login_attempts` 與 `lockout_end_at`。連續 5 次失敗啟動 15 分鐘漸進式鎖定；成功登入立即歸零計數。
- **權杖單一有效性**：
  - 每個 Member 針對特定目的（驗證信箱、重設密碼）維持單一有效 VerificationToken，新申請立即作廢舊未用權杖。
  - 資料庫只儲存單向安全雜湊 (`token_hash`)，防範資料庫洩漏時權杖外洩。
- **API First 開發準則**：
  - 所有端點、DTO 與 RFC 7807 ProblemDetails 錯誤回應均由 OpenAPI 3.0 規格（`docs/specs/member-api-v1.yaml`）驅動。

## 測試決策 (Testing Decisions)

- **單一最高測試接縫政策 (Single High Seam Policy)**：
  - 後端測試嚴格收斂在唯一接縫：**`WebApplicationFactory<Program>` HTTP 端點層級 + Testcontainers (PostgreSQL)**。
  - 排除充滿 Mock 的隔離單元測試。測試僅斷言外部可觀察行為（HTTP 狀態碼、Set-Cookie、回應負載、資料庫狀態變更），不 Mock 內部實作。
- **BDD 規範與事前審核**：
  - 每個 User Story 均透過 Given-When-Then 自動化情境驗收。
  - **測試案例編寫完成後，必須先讓使用者檢視並確認審核，方可開始實作**。
- **前端測試接縫**：
  - 使用 `@vue/test-utils` 與 Vitest 進行組件與 Composable 整合測試，驗證 DOM 狀態、事件發射與 Pinia 身分狀態。

## 明確排除範圍 (Out of Scope)

- 自建 OAuth 2.0 / OIDC 授權發號端點（交由未來獨立的 `identity-server` 模組處理）。
- 手機號碼與簡訊 OTP 驗證登入（此階段以 Email 為唯一主帳號）。
- 多因素驗證 (MFA / TOTP / WebAuthn)。
- 第三方社群帳號登入整合（Google, Apple, Facebook）。
- 企業級 SAML 2.0 / LDAP 連接器。

## 補充說明 (Further Notes)

- Monorepo 目錄劃分：
  - 後端：`apps/member-api/`
  - 前端：`apps/member-web/`
  - 規格文件：`docs/specs/`
  - 架構決策記錄：`docs/adr/`
- 受 ADR 0001 (Testcontainers BDD 測試)、ADR 0002 (Security Stamp 會話註銷) 與 ADR 0003 (Transactional Outbox 郵件派發) 約束。
