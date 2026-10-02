---
計畫模板版本: 2026-07-12
用途: 管理者中心全流程 E2E 測試（應用審核核准/駁回、緊急停用與稽核日誌）
---

# 管理者中心全流程 E2E 測試

**建立日期**: 2026-10-02 19:02 GMT+8  
**狀態**: [進行中]  
**父工單**: [#56](https://github.com/yaochangyu/auth-platform/issues/56)  
**當前工單**: [#60](https://github.com/yaochangyu/auth-platform/issues/60)

⚠️ **檔案命名**: `admin-web-e2e-2026-10-02.plan.md`

## 概覽

- **目標**: 實作管理者中心全流程端到端測試（Issue #60），走通管理員身分認證（透過升級會員角色為 admin）、應用程式列表與狀態篩選、待審核應用程式核准 (Approve)、緊急斷路停用 (Suspend) 與取消停用，以及稽核日誌 (Audit Logs) 查詢驗證。
- **關鍵決策**:
  1. 實作 `e2e/pages/admin-page.ts`（Page Object Model），封裝管理員後台之應用程式列表、狀態篩選、應用程式詳情、審核/停用操作 AlertDialog、以及稽核日誌查詢與卡片解析。
  2. 擴充 `e2e/support/db-helper.ts`（或於測試輔助工具中），提供 `promoteToAdmin(email)` 與 `setApplicationStatus(appId, status)`，透過 `docker compose exec postgres psql` 在整合環境精準設定測試資料前置狀態，模擬真實管理員與待審核案件。
  3. 撰寫 `e2e/specs/admin-web.spec.ts`，驗證：
     - 非管理員訪問管理後台顯示 403 / 拒絕存取。
     - 管理員訪問管理後台，SSO 身分延續並於導覽列正確顯示管理員 Email。
     - 待審核 (PendingReview) 應用程式詳情頁顯示「核准上線」按鈕，點擊後成功轉為 `Active`。
     - 啟用中 (Active) 應用程式填寫停用原因，觸發「緊急斷路停用」，斷言狀態變更為 `Suspended` 並顯示斷路器統計。
     - 進入 `/audit-logs` 查詢，斷言出現 `application.suspend` 與 `application.activate` 稽核日誌，並驗證操作人、事件型態與時間戳記。
- **派工流程**: 實作使用 `agent1`（Sonnet 5.5）實作，審核使用 `agent2`（Gemini 3.8 Flash）雙軸審查，協調者維持全自動閉環。

## 執行步驟

| # | 步驟 | 說明 | 狀態 |
|---|------|------|------|
| 1 | 建立 AdminPage Page Object 與 DB 輔助工具 | 封裝應用程式列表、狀態篩選、審核按鈕、停用彈窗與稽核日誌操作 | ✅ 完成 |
| 2 | 撰寫管理者全流程 E2E 測試案例 | 實作非管員 403 阻擋、管理員登入、審核核准、緊急停用與稽核日誌查詢 | ✅ 完成 |
| 3 | 執行 E2E 測試驗證與全方案回歸 | 確保 npm run test:e2e 通過且 dotnet test 315 項全數綠燈 | ✅ 完成 |

**狀態說明**:
- ⬜ 待做 (Not started)
- 🟦 進行中 (In progress)  
- ✅ 完成 (Completed)
- ⚠️ 阻塞 (Blocked - 需要使用者決定)

## 步驟詳情

### Step 1: 建立 AdminPage Page Object 與 DB 輔助工具

**預期產出**:
- `e2e/pages/admin-page.ts`
- `e2e/support/db-helper.ts`

**完成條件**:
- [x] 封裝導航：`gotoApplications`、`gotoAppDetail(id)`、`gotoAuditLogs(params)`
- [x] 封裝狀態篩選：`filterByStatus(status)`
- [x] 封裝審核操作：`clickApprove`、`suspendApp(reason)`、`clickUnsuspend`
- [x] 封裝稽核日誌：`searchLogs(action, targetId)`、`getLogItems`
- [x] 封裝導覽列身分獲取：`getNavIdentity`
- [x] 實作 `promoteToAdmin(email)` 與 `setApplicationStatus(appId, status)`

---

### Step 2: 撰寫管理者全流程 E2E 測試案例

**預期產出**:
- `e2e/specs/admin-web.spec.ts`

**完成條件**:
- [x] 測試 1：一般會員造訪管理後台遭 403 阻擋或顯示錯誤
- [x] 測試 2：管理員 SSO 登入，導覽列正確顯示管理員 Email，應用程式列表正常渲染
- [x] 測試 3：審核待審核 (PendingReview) 應用程式，點擊「核准上線」變更為 Active
- [x] 測試 4：緊急斷路停用已啟用應用程式，填寫原因並確認彈窗，狀態變更為 Suspended
- [x] 測試 5：稽核日誌查詢，斷言操作人、事件型態（application.suspend / application.activate）正確入庫顯示

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
- [x] 關閉 GitHub Issue #60
- [x] 計畫書已移到 `.archive/` 資料夾

---

**狀態**: [已完成 - 100%]

