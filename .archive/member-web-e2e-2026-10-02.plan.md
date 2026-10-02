---
計畫模板版本: 2026-07-12
用途: 會員中心全流程 E2E 測試（雙軌註冊、OTP 啟用、SSO 登入、個人檔案與登出）
---

# 會員中心全流程 E2E 測試

**建立日期**: 2026-10-02 07:56 GMT+8  
**狀態**: [完成 - 100%]  
**父工單**: [#56](https://github.com/yaochangyu/auth-platform/issues/56)  
**當前工單**: [#58](https://github.com/yaochangyu/auth-platform/issues/58)

⚠️ **檔案命名**: `member-web-e2e-2026-10-02.plan.md`

## 概覽

- **目標**: 實作會員中心全流程端到端測試（Issue #58），走通 Email 註冊、驗證信啟用、手機號碼 OTP 驗證、雙軌登入取得 SSO Session Cookie、個人檔案生日 Write-Once 防弊，以及登出會話失效驗證。
- **關鍵決策**: 
  1. 實作 `e2e/pages/member-page.ts`（Page Object Model），封裝會員註冊、登入、個人檔案檢視與登出之 DOM 操作。
  2. 撰寫 `e2e/specs/member-web.spec.ts`，結合 `MailpitClient` 與 `SmspitClient` 驗證通訊攔截與認證管道。
  3. 驗證 Session Cookie 在 `.1111.com.tw` 網域之核發與登出後的作廢狀態。
  4. 驗證生日 Write-Once 首次 PATCH 成功與二次 409 Conflict 阻斷。
- **派工流程**: 實作使用 `/implement`，審核使用 `/code-review` 雙軸審查，全自動以 `schedule` 鬧鐘喚醒推進。

## 執行步驟

| # | 步驟 | 說明 | 狀態 |
|---|------|------|------|
| 1 | 建立 MemberPage Page Object | 封裝註冊、登入、個人檔案與登出之頁面導航與表單元素互動 | ✅ 完成 |
| 2 | 撰寫會員全流程 E2E 測試案例 | 實作 Email 註冊/啟用、手機 OTP 驗證、登入、生日防弊、登出測試 | ✅ 完成 |
| 3 | 執行 E2E 測試驗證與全方案回歸 | 確保 npm run test:e2e 通過且 dotnet test 315 項全數綠燈 | ✅ 完成 |

**狀態說明**:
- ⬜ 待做 (Not started)
- 🟦 進行中 (In progress)  
- ✅ 完成 (Completed)
- ⚠️ 阻塞 (Blocked - 需要使用者決定)

## 步驟詳情

### Step 1: 建立 MemberPage Page Object

**預期產出**:
- `e2e/pages/member-page.ts`

**完成條件**:
- [x] 封裝 `gotoRegister`、`fillRegisterForm`、`submitRegister`
- [x] 封裝 `gotoLogin`、`fillLoginForm`、`submitLogin`
- [x] 封裝 `gotoProfile`、`getProfileEmail`、`clickLogout`
- [x] 封裝 `gotoVerifyEmail(token)`

---

### Step 2: 撰寫會員全流程 E2E 測試案例

**預期產出**:
- `e2e/specs/member-web.spec.ts`

**完成條件**:
- [x] 測試 1：Email 註冊 ➔ Mailpit 取得 Token ➔ 啟用 ➔ 登入成功 ➔ 驗證 Session Cookie
- [x] 測試 2：手機號碼註冊 ➔ Smspit 取得 OTP ➔ 手機號碼驗證 ➔ 登入成功
- [x] 測試 3：個人檔案生日增量補填（首次 200，二次覆寫 409 Conflict）
- [x] 測試 4：會員登出 ➔ 驗證 Session Cookie 作廢並導回登入頁

---

### Step 3: 執行 E2E 測試驗證與全方案回歸

**預期產出**:
- 綠燈測試報告
- Git Commit

**完成條件**:
- [x] `npm run test:e2e` 全數綠燈通過
- [x] `dotnet test AuthPlatform.slnx` 315 項維持 100% 綠燈
- [x] Commit 格式規範（無 `Co-authored-by`）

---

## 完成檢查表

計畫完成時執行：

- [x] 所有步驟狀態都是 ✅ 完成
- [x] 已執行 `npm run test:e2e` 綠燈通過
- [x] 已執行 `dotnet test` 全方案測試通過
- [x] 經雙軸審核放行後 commit 到 git
- [x] 關閉 GitHub Issue #58
- [x] 計畫書已移到 `.archive/` 資料夾

---

**狀態**: [已完成 - 100%]

