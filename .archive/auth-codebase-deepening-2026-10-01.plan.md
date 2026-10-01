---
計畫模板版本: 2026-07-12
用途: 身分驗證核心深度模組化與接縫整併
---

# 身分驗證核心深度模組化與接縫整併

**建立日期**: 2026-10-01 10:07 GMT+8  
**狀態**: [完成 - 100%]  
**父工單**: [#42](https://github.com/yaochangyu/auth-platform/issues/42)

⚠️ **檔案命名**: `auth-codebase-deepening-2026-10-01.plan.md`

## 概覽

- **目標**: 實作 ADR 0011 拍板之架構決策：建立領域純邏輯深模組 `LoginLockoutPolicy` 集中登入安全知識、收斂 Member API 虛擬接縫（刪除單一實作的 `I*Handler`）、以及統一 Auth Web 狀態管理至 Pinia Store。
- **關鍵決策**: 
  1. 建立領域純邏輯 `LoginLockoutPolicy`，以純函式與單元測試保障鎖定時限邊界，讓 Repository 回歸純資料存取。
  2. 依照 The Deletion Test，拔除 7 個單一實作的 Handler 介面，Controller 直接依賴具體類別。
  3. 前端狀態統一由 Pinia `useAuthStore` 維護，實現登出原子化清空。
- **派工流程**: 實作使用 `/implement` + `api.template` 規範，審核使用 `/code-review`，實施「審核 ➔ 抗辯 ➔ 仲裁」機制。

## 執行步驟

| # | 步驟 | 說明 | 狀態 |
|---|------|------|------|
| 1 | 建立 LoginLockoutPolicy 領域深模組與純單元測試 (#43) | 建立純邏輯政策模型與單元測試，收斂鎖定規則並改寫 LoginHandler 與 MemberRepository | ✅ 完成 |
| 2 | 收斂 Member API 虛擬接縫（刪除單一實作 I*Handler） (#44) | 刪除 7 個無多態價值的虛擬介面，簡化 AuthController 注入與 Program.cs DI 註冊 | ✅ 完成 |
| 3 | 整合 Auth Web 前端狀態至 Pinia Store (#45) | 將登入態、登出邏輯與 Profile 集中由 Pinia 管理，確保登出狀態原子化重設 | ✅ 完成 |
| 4 | 全套測試與雙軸審查驗收 | 執行完整單元、整合與煙霧測試，驗收無回歸並啟動 /code-review 雙軸審查 | ✅ 完成 |

**狀態說明**:
- ⬜ 待做 (Not started)
- 🟦 進行中 (In progress)  
- ✅ 完成 (Completed)
- ⚠️ 阻塞 (Blocked - 需要使用者決定)

## 步驟詳情

### Step 1: 建立 LoginLockoutPolicy 領域深模組與純單元測試 (#43)

**預期產出**:
- `apps/member-api/Domain/LoginLockoutPolicy.cs`
- `tests/MemberApi.Tests/Domain/LoginLockoutPolicyTests.cs`
- 修改 `LoginHandler.cs` 與 `MemberRepository.cs`

**完成條件**:
- [x] 實作純邏輯 `LoginLockoutPolicy`（5 次上限、15 分鐘鎖定、過期重設）
- [x] 完成單元測試，邊界條件 100% 覆蓋
- [x] `LoginHandler` 與 `MemberRepository` 改用該政策模型
- [x] `dotnet test` 通過

**進度**:
```
✅ 完成
```

---

### Step 2: 收斂 Member API 虛擬接縫（刪除單一實作 I*Handler） (#44)

**預期產出**:
- 刪除 `apps/member-api/Handlers/` 下的 7 個 `I*Handler.cs` 介面檔案
- 更新 `apps/member-api/Controllers/AuthController.cs` 建構函式依賴
- 更新 `apps/member-api/Program.cs` DI 註冊

**完成條件**:
- [x] 刪除 `IRegisterMemberHandler`, `IVerifyEmailHandler`, `ILoginHandler`, `IForgotPasswordHandler`, `IResetPasswordHandler`, `ISendSmsOtpHandler`, `IVerifyPhoneHandler`
- [x] `AuthController` 直接注入具體 Handler 類別
- [x] `Program.cs` 註冊 Concrete Handlers
- [x] `dotnet build` 零錯誤，全套測試通過

**進度**:
```
✅ 完成
```

---

### Step 3: 整合 Auth Web 前端狀態至 Pinia Store (#45)

**預期產出**:
- `apps/member-web/src/stores/auth.ts`
- `apps/member-web/src/composables/useAuth.ts`
- `apps/member-web/src/views/LoginView.vue` 等視圖調用對齊

**完成條件**:
- [x] `useAuthStore` 納入 `login`, `register`, `logout`, `loginResult` 狀態與方法
- [x] 登出時執行 `$reset()` 清空所有登入與 Profile 狀態
- [x] 前端 TypeScript 型別檢查 `npm run type-check` 或 `vue-tsc` 通過

**進度**:
```
✅ 完成
```

---

### Step 4: 全套測試與雙軸審查驗收

**預期產出**:
- 全方案 314 項整合與 BDD 測試維持 100% 綠燈
- 雙軸審查（Standards 軸與 Spec 軸）報告

**完成條件**:
- [x] `dotnet test AuthPlatform.slnx` 314 項測試全部通過
- [x] 前端構建與型別檢查通過
- [x] 啟動 `/code-review` 審查 Pass

**進度**:
```
✅ 完成
```

---

## 遭遇的問題

無。

---

## 完成檢查表

計畫完成時執行：

- [x] 所有步驟狀態都是 ✅ 完成
- [x] 已執行 `dotnet build` 驗證
- [x] 已執行測試
- [x] 已 commit 到 git
- [x] 計畫書已移到 `.archive/` 資料夾

---

**狀態**: 已完成
