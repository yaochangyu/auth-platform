---
計畫模板版本: 2026-07-12
用途: 後端架構收斂：消除假共用與無效繼承
---

# 後端架構收斂：消除假共用與無效繼承

**建立日期**: 2026-10-02 00:13 GMT+8  
**狀態**: [已完成 - 100%]  
**父工單**: [#53](https://github.com/yaochangyu/auth-platform/issues/53)

⚠️ **檔案命名**: `backend-decoupling-and-seam-pruning-2026-10-02.plan.md`

## 概覽

- **目標**: 依據 `codebase-design` 原則（真實接縫 vs 假設接縫、Refused Bequest 消除、單一適配器原則），消除後端「為了共用而共用」的過早通用化代碼：將單一適配器的 M2M HMAC 驗簽模組由 `auth-shared` 收斂至 `developer-api` 內部；並將未調用保護方法的 `M2mController` 與 `AuditLogsController` 改為直接繼承 `ControllerBase`。
- **關鍵決策**: 
  1. `apps/auth-shared/Hmac/` 完整搬移至 `apps/developer-api/Security/Hmac/`，刪除原共用目錄，更新 DI 註冊。
  2. `M2mController` 與 `AuditLogsController` 改為繼承 `ControllerBase`，移除不必要的 `ApiControllerBase`。
  3. 全方案測試維持 100% 綠燈，前後端無任何破壞性變更。
- **派工流程**: 實作使用 `/implement` + `api.template` 規範，審核使用 `/code-review`，實施「審核 ➔ 抗辯 ➔ 仲裁」機制，全自動以 `schedule` 鬧鐘喚醒推進。

## 執行步驟

| # | 步驟 | 說明 | 狀態 |
|---|------|------|------|
| 1 | 將 M2M HMAC 驗簽模組由 auth-shared 收斂至 developer-api 內部 (#54) | 搬移 Hmac 檔案至 developer-api，更新命名空間與 DI 註冊，刪除 auth-shared/Hmac，測試通過 | ✅ 完成 |
| 2 | Controller 繼承層次瘦身，消除被拒絕的遺贈 (Refused Bequest) (#55) | M2mController 與 AuditLogsController 改為繼承 ControllerBase，全方案測試通過 | ✅ 完成 |

**狀態說明**:
- ⬜ 待做 (Not started)
- 🟦 進行中 (In progress)  
- ✅ 完成 (Completed)
- ⚠️ 阻塞 (Blocked - 需要使用者決定)

## 步驟詳情

### Step 1: 將 M2M HMAC 驗簽模組由 auth-shared 收斂至 developer-api 內部 (#54)

**預期產出**:
- `apps/developer-api/Security/Hmac/HmacAuthentication.cs`
- `apps/developer-api/Security/Hmac/HmacSignature.cs`
- `apps/developer-api/Program.cs`
- `apps/developer-api/Controllers/M2mController.cs`
- `apps/developer-api/Security/ApiKeyHmacResolver.cs`
- 移除 `apps/auth-shared/Hmac/`

**完成條件**:
- [x] 搬移檔案至 `developer-api`，調整命名空間為 `DeveloperApi.Security.Hmac`
- [x] `auth-shared` 刪除 `Hmac/` 目錄
- [x] `developer-api` 內 DI 註冊與引用調整完成
- [x] `dotnet test tests/DeveloperApi.Tests` 通過
- [x] 經審核放行後 commit (f819b95)

**進度**:
```
✅ 完成 (Commit: f819b95)
```

---

### Step 2: Controller 繼承層次瘦身，消除被拒絕的遺贈 (Refused Bequest) (#55)

**預期產出**:
- `apps/developer-api/Controllers/M2mController.cs`
- `apps/admin-api/Controllers/AuditLogsController.cs`

**完成條件**:
- [x] `M2mController` 改為繼承 `ControllerBase`
- [x] `AuditLogsController` 改為繼承 `ControllerBase`
- [x] 全方案 315 項測試維持 100% 綠燈
- [x] 經審核放行後 commit (fd67837)

**進度**:
```
✅ 完成 (Commit: fd67837)
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

**狀態**: 已完成

