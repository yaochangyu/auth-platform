---
計畫模板版本: 2026-07-12
用途: 開發者中心全流程 E2E 測試（SSO 身分延續、OAuth 應用建立、金鑰發行）
---

# 開發者中心全流程 E2E 測試

**建立日期**: 2026-10-02 18:41 GMT+8  
**狀態**: [進行中]  
**父工單**: [#56](https://github.com/yaochangyu/auth-platform/issues/56)  
**當前工單**: [#59](https://github.com/yaochangyu/auth-platform/issues/59)

⚠️ **檔案命名**: `developer-web-e2e-2026-10-02.plan.md`

## 概覽

- **目標**: 實作開發者中心全流程端到端測試（Issue #59），驗證在已有會員登入態下的跨網域 SSO 身分延續（免重複登入）、建立應用程式並列表顯示 Active 狀態、OAuth 設定（Redirect URI、Confidential/Public 切換）、發行 Client Secret（明文 Alert 警告與複製），以及發行 API Key（`ak_test_` 前綴、明文 Alert 警告與複製）。
- **關鍵決策**:
  1. 實作 `e2e/pages/developer-page.ts`（Page Object Model），封裝開發者後台之應用程式列表、建立表單、OAuth 設定、Client Secret 面板與 API Key 表單/彈窗操作。
  2. 撰寫 `e2e/specs/developer-web.spec.ts`，利用 `MemberPage` 建立並登入會員後，直接開啟 `http://developer.1111.com.tw:5105/apps`，透過 OpenIddict 自動授權無感回跳，斷言導覽列正確顯示會員 Email。
  3. 驗證應用程式建立表單（名稱、簡介、聯絡信箱），提交後列表與詳情正確呈現 `Active` 狀態。
  4. 驗證 OAuth Client 設定：新增 Redirect URI、切換 Public/Confidential 儲存。
  5. 驗證金鑰發行：產生 Client Secret，斷言「請立即複製」警告與剪貼簿複製；發行 `ak_test_` 測試機 API Key，斷言金鑰前綴與複製按鈕。
- **派工流程**: 實作使用 `agent1`（Sonnet 5.5）實作，審核使用 `agent2`（Gemini 3.8 Flash）雙軸審查，協調者維持全自動閉環。

## 執行步驟

| # | 步驟 | 說明 | 狀態 |
|---|------|------|------|
| 1 | 建立 DeveloperPage Page Object | 封裝應用程式列表、建立表單、OAuth 設定、Client Secret 與 API Key 發行互動 | 🟦 進行中 |
| 2 | 撰寫開發者全流程 E2E 測試案例 | 實作 SSO 身分延續、建立 App、OAuth 設定、Secret 發行、API Key 發行測試 | ⬜ 待做 |
| 3 | 執行 E2E 測試驗證與全方案回歸 | 確保 npm run test:e2e 通過且 dotnet test 315 項全數綠燈 | ⬜ 待做 |

**狀態說明**:
- ⬜ 待做 (Not started)
- 🟦 進行中 (In progress)  
- ✅ 完成 (Completed)
- ⚠️ 阻塞 (Blocked - 需要使用者決定)

## 步驟詳情

### Step 1: 建立 DeveloperPage Page Object

**預期產出**:
- `e2e/pages/developer-page.ts`

**完成條件**:
- [ ] 封裝導航：`gotoApps`、`gotoNewApp`、`gotoAppDetail`、`gotoOAuthSettings`、`gotoApiKeys`
- [ ] 封裝建立應用程式表單互動：`fillAppForm`、`submitAppForm`
- [ ] 封裝 OAuth 設定：`fillOAuthForm`、`submitOAuthForm`
- [ ] 封裝 Client Secret：`issueClientSecret`、`copyClientSecret`、`dismissClientSecret`
- [ ] 封裝 API Key：`issueApiKey`、`copyApiKey`、`dismissApiKey`
- [ ] 封裝導覽列身分獲取：`getNavIdentity`

---

### Step 2: 撰寫開發者全流程 E2E 測試案例

**預期產出**:
- `e2e/specs/developer-web.spec.ts`

**完成條件**:
- [ ] 測試 1：在會員已登入態下開啟開發者中心，驗證 SSO 自動跳轉並於 Navbar 顯示會員 Email
- [ ] 測試 2：建立應用程式，驗證成功跳轉詳情頁並於列表中正確顯示名稱與 Active 狀態
- [ ] 測試 3：OAuth 設定變更，填寫 Redirect URI 並儲存成功
- [ ] 測試 4：發行 Client Secret，斷言出現明文提示與複製按鈕正常運作
- [ ] 測試 5：發行 API Key（測試環境），斷言產生 `ak_test_` 前綴金鑰並可正常複製

---

### Step 3: 執行 E2E 測試驗證與全方案回歸

**預期產出**:
- 綠燈測試報告
- Git Commit

**完成條件**:
- [ ] `npm run test:e2e` 全數綠燈通過
- [ ] `dotnet test AuthPlatform.slnx` 315 項維持 100% 綠燈
- [ ] Commit 格式規範（無 `Co-authored-by`）

---

## 完成檢查表

計畫完成時執行：

- [ ] 所有步驟狀態都是 ✅ 完成
- [ ] 已執行 `npm run test:e2e` 綠燈通過
- [ ] 已執行 `dotnet test` 全方案測試通過
- [ ] 經雙軸審核放行後 commit 到 git
- [ ] 關閉 GitHub Issue #59
- [ ] 計畫書已移到 `.archive/` 資料夾

---

**狀態**: 執行中
