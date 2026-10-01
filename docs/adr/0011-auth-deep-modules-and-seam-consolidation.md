# ADR 0011：身分驗證核心深度模組化與接縫整併 (Deep Modules & Seam Consolidation)

## 狀態
已拍板 (Accepted) - 2026-10-01

## 背景
經過 `/codebase-design` 對系統架構（`apps/auth-server`、`apps/member-api`、`apps/member-web`）的深度審視，發現以下架構淺薄（Shallow Modules）、知識分散（Locality 破壞）與狀態脫節的問題：
1. **虛擬接縫氾濫 (Hypothetical Seams)**：
   `apps/member-api` 的 `AuthController` 建構函式注入多達 14 個依賴，包含 7 個 `I*Handler` 介面（如 `IRegisterMemberHandler`、`ILoginHandler` 等）。每個介面皆僅有唯一實作，且僅做單向轉手，違反「One adapter means a hypothetical seam. Two adapters means a real one」原則，通過 Deletion Test 判定為無多態價值的虛擬接縫。
2. **登入失敗鎖定規則知識分散**：
   登入失敗次數上限（5 次）與鎖定時長（15 分鐘）常數定義於 `LoginHandler`，但判斷「鎖定是否已過期需重設」的業務邏輯又分散在 `MemberRepository` 的原生 SQL 中，知識散落兩處。
3. **前端認證狀態多來源脫節**：
   `apps/member-web` 前端將登入結果暫存於模組層級的 Composable（`useAuth.ts`），而會員 Profile 卻存放於 Pinia Store（`stores/auth.ts`）。使用者登出時僅調用後端與 Composable 變數，未連動清空 Pinia Profile，破壞前端狀態真實性。

## 決策內容 (Decisions)

### 1. 收斂 Member API 虛擬接縫（刪除單一實作的 `I*Handler` 介面）
- 刪除僅有單一實作的 7 個 `I*Handler` 抽象介面（`IRegisterMemberHandler`, `IVerifyEmailHandler`, `ILoginHandler`, `IForgotPasswordHandler`, `IResetPasswordHandler`, `ISendSmsOtpHandler`, `IVerifyPhoneHandler`）。
- `AuthController` 與 DI 容器直接註冊並注入具體 Handler 類別（Concrete Handlers）。
- 保留 Handler 本身的用例職責劃分，精簡依賴定義，提升代碼導航與維護槓桿。

### 2. 建立領域純邏輯深模組 LoginLockoutPolicy
- 於 `MemberApi.Domain` 建立純邏輯的 `LoginLockoutPolicy` 領域模型。
- 將連續失敗計數累加、鎖定時間計算、是否處於鎖定狀態、鎖定是否過期重設等邏輯完整收斂至該模型。
- `MemberRepository` 僅作為儲存接縫（Persistence Seam），只負責將狀態寫入與讀出，不再內嵌業務時間與次數規則運算。
- **仲裁決議（並行與交易邊界）**：Persistence Seam 可以在內部持有僅限單一原子操作的交易（例如為確保 `FOR UPDATE` 列鎖完整覆蓋讀取與寫回），但不可以跨越外部業務流程。呼叫端無需知曉或介入交易細節，維持深模組之高槓桿。

### 3. Auth Web 前端狀態收斂至 Pinia Store
- 將登入（`login`）、註冊（`register`）、登出（`logout`）、登入結果與 Profile 狀態統一由 Pinia `useAuthStore` 集中管理。
- 登出時執行原子化清空（`$reset()`），確保元件視圖、路由守衛與後端 Session 完全一致。

### 4. Auth Server 保持最簡防重放機制
- 依據 /ponytail 原則，`ConsentTicketService` 保持基於 Data Protection 加密之票證與記憶體已消耗紀錄，在未進行多節點叢集部署前，不預先引入 Redis 或額外資料庫寫入開銷。

## 後果 (Consequences)

- **正面效益**：
  1. 消除 7 個無實質價值的介面檔案與虛擬 Seam，顯著簡化 `AuthController` 建構函式。
  2. 登入失敗鎖定政策集中於單一領域物件，大幅提升單元測試覆蓋度與修改局部性（Locality）。
  3. 前端認證狀態統一收歸 Pinia，杜絕登出殘留與狀態不同步的 Bug。
- **承擔代價**：
  1. 移除 `I*Handler` 介面後，若有針對 Controller 的純單元測試需改以整合測試（WebApplicationFactory）或直接實例化 Concrete Handler 測試。
