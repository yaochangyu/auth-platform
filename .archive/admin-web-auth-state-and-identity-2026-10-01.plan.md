---
計畫模板版本: 2026-07-12
用途: 管理後台認證狀態深模組化、身分顯示與全鏈路登出
---

# 管理後台認證狀態深模組化、身分顯示與全鏈路登出

**建立日期**: 2026-10-01 18:58 GMT+8  
**狀態**: [已完成 - 100%]  
**父工單**: [#46](https://github.com/yaochangyu/auth-platform/issues/46)

⚠️ **檔案命名**: `admin-web-auth-state-and-identity-2026-10-01.plan.md`

## 概覽

- **目標**: 實作管理後台前端認證狀態收斂（Pinia `useAuthStore`）、OIDC UserInfo 身分讀取與展示、以及雙軌原子化 Single Sign-Out，消除前端狀態分裂與共用裝置會話殘留安全風險。
- **關鍵決策**: 
  1. `auth-server` 在種子設定為 `admin-web` 補上 `Scopes.Email`，提供合法讀取 Email 之 OIDC 權限。
  2. `apps/admin-web/src/stores/auth.ts` 集中掌管 `accessToken`、`profile`、`error` 與登入生命週期；`useOAuth.ts` 改為純 Facade。
  3. `AppHeader.vue` 顯示管理員身分並提供登出按鈕；登出採用 `try...finally` 保證原子化清空本地 Store 並呼叫 `member-api` 註銷跨站 Session Cookie。
- **派工流程**: 實作使用 `/implement` + `api.template` 規範，審核使用 `/code-review`，實施「審核 ➔ 抗辯 ➔ 仲裁」機制，全自動以 `schedule` 鬧鐘喚醒推進。

## 執行步驟

| # | 步驟 | 說明 | 狀態 |
|---|------|------|------|
| 1 | 授權伺服器 Client 種子範疇擴充與 OIDC 支援 (#47) | ClientSeeder 追加 Scopes.Email，並確認測試與 Token 簽發符合 OIDC 規範 | ✅ 完成 |
| 2 | 管理後台認證狀態收斂至 Pinia Store 與 Facade 委派 (#48) | 擴充 useAuthStore 納入 profile 與生命週期，useOAuth 轉為 Facade，前端建置通過 | ✅ 完成 |
| 3 | 管理導覽列身分呈現與雙軌全鏈路登出 (Single Sign-Out) (#49) | AppHeader 展示身分與登出按鈕，logout() 原子化清空並註銷 Cookie，全套測試驗收 | ✅ 完成 |

**狀態說明**:
- ⬜ 待做 (Not started)
- 🟦 進行中 (In progress)  
- ✅ 完成 (Completed)
- ⚠️ 阻塞 (Blocked - 需要使用者決定)

## 步驟詳情

### Step 1: 授權伺服器 Client 種子範疇擴充與 OIDC 支援 (#47)

**預期產出**:
- 修改 `apps/auth-server/Infrastructure/ClientSeeder.cs`
- 驗證或補充測試案例
- 全方案測試通過

**完成條件**:
- [x] `admin-web` Scope 包含 `Scopes.Email`
- [x] `dotnet test AuthPlatform.slnx` 315 項測試通過
- [x] 經審核放行後 commit (9616b8b)

**進度**:
```
✅ 完成 (Commit 9616b8b)
```

---

### Step 2: 管理後台認證狀態收斂至 Pinia Store 與 Facade 委派 (#48)

**預期產出**:
- `apps/admin-web/src/stores/auth.ts`
- `apps/admin-web/src/composables/useOAuth.ts`
- `apps/admin-web/src/views/OAuthCallbackView.vue` 等調用對齊

**完成條件**:
- [x] `useAuthStore` 統一管理認證、Token 與 Profile
- [x] `useOAuth.ts` 轉為 Facade
- [x] 前端 `npm run build` 通過
- [x] 經審核放行後 commit (014bbd4)

**進度**:
```
✅ 完成 (Commit 014bbd4)
```

---

### Step 3: 管理導覽列身分呈現與雙軌全鏈路登出 (Single Sign-Out) (#49)

**預期產出**:
- `apps/admin-web/src/components/AppHeader.vue`
- `apps/admin-web/src/stores/auth.ts` 中的 `logout()` 實作
- 端到端驗收

**完成條件**:
- [x] `AppHeader.vue` 呈現身分與登出按鈕
- [x] 登出原子化清空並註銷 Cookie
- [x] 前端 build 通過，全方案 315 項測試綠燈
- [x] 經審核放行後 commit (d10c862)

**進度**:
```
✅ 完成 (Commit d10c862)
```

---

## 完成檢查表

計畫完成時執行：

- [x] 所有步驟狀態都是 ✅ 完成
- [x] 已執行 `dotnet build` 與前端建置驗證
- [x] 已執行全方案測試 (315/315 通過)
- [x] 已 commit 到 git
- [x] 計畫書已移到 `.archive/` 資料夾

---

**狀態**: 已完成

