---
計畫模板版本: 2026-07-12
用途: Playwright E2E 測試基礎架構、通訊 Client 與首頁健康檢查 Tracer Bullet
---

# Playwright E2E 測試基礎架構與通訊 Client

**建立日期**: 2026-10-02 07:40 GMT+8  
**狀態**: [完成 - 100%]  
**父工單**: [#56](https://github.com/yaochangyu/auth-platform/issues/56)  
**當前工單**: [#57](https://github.com/yaochangyu/auth-platform/issues/57)

⚠️ **檔案命名**: `playwright-e2e-foundation-2026-10-02.plan.md`

## 概覽

- **目標**: 為 auth-platform 建立以 Playwright 為核心的跨瀏覽器端到端（E2E）測試工程體系（Issue #57），實作免改 hosts 的 Chromium 網域解析參數、封裝 Mailpit/Smspit 通訊驗證客戶端，並交付第一個端到端 Tracer Bullet 健康檢查測試。
- **關鍵決策**: 
  1. 根目錄配置 `package.json`，提供 `test:e2e` 指令，測試程式碼收斂至 `e2e/` 目錄，不污染任何前端子專案。
  2. Chromium 啟動參數配置 `--host-resolver-rules="MAP *.1111.com.tw 127.0.0.1"`，無需本機 sudo 權限或改動 `/etc/hosts`。
  3. 實作 `MailpitClient` 與 `SmspitClient` 支援郵件 Token 與簡訊 OTP 自動輪詢與正則解析。
  4. 實作 `e2e/specs/health-smoke.spec.ts` 驗證三大 SPA 首頁渲染與後端 API 連通。
- **派工流程**: 實作使用 `/implement`，審核使用 `/code-review` 雙軸審查，全自動以 `schedule` 鬧鐘喚醒推進。

## 執行步驟

| # | 步驟 | 說明 | 狀態 |
|---|------|------|------|
| 1 | 專案根目錄配置 Playwright 依賴與 playwright.config.ts | 建立 package.json、playwright.config.ts、tsconfig.e2e.json，配置 Chromium 網域解析規則 | 🟦 進行中 |
| 2 | 實作 MailpitClient 與 SmspitClient 通訊輔助工具 | 封裝 REST API 輪詢、郵件 Token / 簡訊 OTP 正則萃取與 test fixtures | ⬜ 待做 |
| 3 | 實作 Tracer Bullet 健康檢查煙霧測試並驗證執行 | 撰寫 health-smoke.spec.ts，執行 npm run test:e2e 通過驗證，確保 dotnet test 315 項不受影響 | ⬜ 待做 |

**狀態說明**:
- ⬜ 待做 (Not started)
- 🟦 進行中 (In progress)  
- ✅ 完成 (Completed)
- ⚠️ 阻塞 (Blocked - 需要使用者決定)

## 步驟詳情

### Step 1: 專案根目錄配置 Playwright 依賴與 playwright.config.ts

**預期產出**:
- `package.json`
- `playwright.config.ts`
- `tsconfig.e2e.json`
- `.gitignore` (更新忽略目錄)

**完成條件**:
- [ ] 根目錄 `package.json` 配置 `@playwright/test` 與腳本
- [ ] `playwright.config.ts` 配置 Chromium `--host-resolver-rules`
- [ ] `npm install` 與 `npx playwright install chromium` 成功
- [ ] `.gitignore` 包含 `test-results/` 與 `playwright-report/`

---

### Step 2: 實作 MailpitClient 與 SmspitClient 通訊輔助工具

**預期產出**:
- `e2e/support/mailpit-client.ts`
- `e2e/support/smspit-client.ts`
- `e2e/support/test-fixtures.ts`

**完成條件**:
- [ ] `MailpitClient` 支援 `waitForToken` 與 `clearMessages`
- [ ] `SmspitClient` 支援 `waitForOtp` 與 `clearMessages`
- [ ] `test-fixtures.ts` 注入 fixture

---

### Step 3: 實作 Tracer Bullet 健康檢查煙霧測試並驗證執行

**預期產出**:
- `e2e/specs/health-smoke.spec.ts`

**完成條件**:
- [ ] 走訪 Member (8080)、Developer (8092)、Admin (8093) 首頁正常渲染
- [ ] 驗證 AuthServer (8091) Discovery 端點
- [ ] 驗證 Mailpit (:8025) 與 Smspit (:8026) 連線
- [ ] `npm run test:e2e` 100% 綠燈通過
- [ ] `dotnet test AuthPlatform.slnx` 315 項維持全數通過

---

## 完成檢查表

計畫完成時執行：

- [ ] 所有步驟狀態都是 ✅ 完成
- [ ] 已執行 `npm run test:e2e` 綠燈通過
- [ ] 已執行 `dotnet test` 全方案測試通過
- [ ] 經雙軸審核放行後 commit 到 git
- [ ] 關閉 GitHub Issue #57
- [ ] 計畫書已移到 `.archive/` 資料夾

---

**狀態**: 待確認
