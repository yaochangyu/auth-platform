---
計畫模板版本: 2026-07-12
用途: 開發者後台認證狀態深模組化、身分顯示與全鏈路登出
---

# 開發者後台認證狀態深模組化、身分顯示與全鏈路登出

**建立日期**: 2026-10-01 22:52 GMT+8  
**狀態**: [已完成 - 100%]  
**父工單**: [#50](https://github.com/yaochangyu/auth-platform/issues/50)

⚠️ **檔案命名**: `developer-web-auth-state-and-identity-2026-10-01.plan.md`

## 概覽

- **目標**: 實作開發者後台前端認證狀態收斂（Pinia `useAuthStore`）、OIDC UserInfo 身分讀取與展示、以及雙軌原子化 Single Sign-Out，消除前端狀態分裂與共用裝置會話殘留安全風險。
- **關鍵決策**: 
  1. `apps/developer-web/src/stores/auth.ts` 集中掌管 `accessToken`、`profile`、`error`、`isLoading` 與登入生命週期；`useOAuth.ts` 改為純 Facade。
  2. 去除 Middle Man：改寫 `useApi.ts` 直連 Store。
  3. `AppHeader.vue` 顯示開發者身分並提供登出按鈕；登出採用 `try...finally` 保證原子化清空本地 Store 並呼叫 `member-api` 註銷跨站 Session Cookie。
  4. `nginx.conf` 與 `vite.config.ts` 補齊 `/connect/userinfo` 與 `/api/v1/auth/logout` 同源反向代理。
- **派工流程**: 實作使用 `/implement` + `api.template` 規範，審核使用 `/code-review`，實施「審核 ➔ 抗辯 ➔ 仲裁」機制，全自動以 `schedule` 鬧鐘喚醒推進。

## 執行步驟

| # | 步驟 | 說明 | 狀態 |
|---|------|------|------|
| 1 | 開發者後台認證狀態收斂至 Pinia Store、UserInfo 整合與 Facade 委派 (#51) | 擴充 useAuthStore 納入 profile 與生命週期，useOAuth 轉為 Facade，useApi 直連 Store，代理補齊，前端建置通過 | ✅ 完成 |
| 2 | 開發者導覽列身分呈現與雙軌全鏈路登出 (Single Sign-Out) (#52) | AppHeader 展示身分與登出按鈕，logout() 原子化清空並註銷 Cookie，全套測試驗收 | ✅ 完成 |

**狀態說明**:
- ⬜ 待做 (Not started)
- 🟦 進行中 (In progress)  
- ✅ 完成 (Completed)
- ⚠️ 阻塞 (Blocked - 需要使用者決定)

## 步驟詳情

### Step 1: 開發者後台認證狀態收斂至 Pinia Store、UserInfo 整合與 Facade 委派 (#51)

**預期產出**:
- `apps/developer-web/src/stores/auth.ts`
- `apps/developer-web/src/composables/useOAuth.ts`
- `apps/developer-web/src/composables/useApi.ts`
- `apps/developer-web/nginx.conf` 與 `vite.config.ts`

**完成條件**:
- [x] `useAuthStore` 統一管理認證、Token 與 Profile
- [x] Scope 包含 `openid profile email developer_api`
- [x] 換票後非同步調用 `fetchProfile()`
- [x] `useOAuth.ts` 轉為 Facade，`useApi.ts` 直連 Store
- [x] 前端 `npm --prefix apps/developer-web run build` 通過
- [x] 經審核放行後 commit (1d0ce46)

**進度**:
```
✅ 完成
```

---

### Step 2: 開發者導覽列身分呈現與雙軌全鏈路登出 (Single Sign-Out) (#52)

**預期產出**:
- `apps/developer-web/src/components/AppHeader.vue`
- `apps/developer-web/src/stores/auth.ts` 中的 `logout()` 實作
- `apps/developer-web/nginx.conf` 與 `vite.config.ts` 登出端點代理
- 端到端驗收

**完成條件**:
- [x] `AppHeader.vue` 呈現身分與登出按鈕
- [x] 登出原子化清空並註銷 Cookie
- [x] 前端 build 通過，全方案 315 項測試綠燈
- [x] 經審核放行後 commit (5165c34)

**進度**:
```
✅ 完成
```

---

## 完成檢查表

計畫完成時執行：

- [x] 所有步驟狀態都是 ✅ 完成
- [x] 已執行 `dotnet build` 與前端建置驗證
- [x] 已執行全方案測試
- [x] 已 commit 到 git
- [x] 計畫書已移到 `.archive/` 資料夾

---

**狀態**: 已完成 (100%)
