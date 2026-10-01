# 後端架構收斂規格書：消除假共用與無效繼承 (Backend Decoupling & Seam Pruning Spec)

## 1. 概述 (Overview)

本規格書依據 [`codebase-design`](../../.gemini/config/skills/codebase-design/SKILL.md) 核心準則（**真實接縫 vs 假設接縫**、**單一適配器原則 One-Adapter Rule**、**刪除測試 Deletion Test** 與 **被拒絕的遺贈 Refused Bequest**），針對後端架構進行精準減法重構：
1. **消除假設性接縫**：將僅有單一適配器（One Adapter）的 M2M HMAC 驗簽邏輯，從跨服務共用庫 `apps/auth-shared` 移回其專屬之 `apps/developer-api` 內部，消除過早通用化（Premature Generalization）。
2. **精簡控制器繼承層次**：將並未調用任何保護方法的控制器（`M2mController` 與 `AuditLogsController`）改為直接繼承 ASP.NET Core 原生 `ControllerBase`，消除物件導向反模式——被拒絕的遺贈（Refused Bequest）。

---

## 2. 領域與接縫分析 (Domain & Seam Analysis)

### 2.1 假設性接縫檢驗：M2M HMAC 驗簽
- **現狀**：
  [`apps/auth-shared/Hmac/HmacAuthentication.cs`](../apps/auth-shared/Hmac/HmacAuthentication.cs) 與 [`HmacSignature.cs`](../apps/auth-shared/Hmac/HmacSignature.cs) 位於全平台共用類別庫。
- **違反準則**：
  「*One adapter means a hypothetical seam. Two adapters means a real one.*」
  全方案中只有 `apps/developer-api` 的 `M2mController` 需要驗證 M2M API Key 簽章；`auth-server`、`admin-api`、`member-api` 完全不需要也不曾調用 HMAC 驗證。
- **改善方向**：
  搬遷至 `apps/developer-api/Security/Hmac/`，調整命名空間為 `DeveloperApi.Security.Hmac`。`auth-shared` 徹底移除 `Hmac/` 目錄，收斂其表面積僅服務於真實的跨服務契約（`OpenIddictStoreDbContext`、`ClientSecrets`、`ApplicationStatus`）。

### 2.2 被拒絕的遺贈檢驗：ApiControllerBase 繼承
- **現狀**：
  [`apps/auth-shared/Web/ApiControllerBase.cs`](../apps/auth-shared/Web/ApiControllerBase.cs) 提供 `CurrentMemberId()`、`NotFoundProblem()`、`ConflictProblem()` 與 `ToValidationProblem()` 等保護方法。
- **違反準則**：
  - `apps/developer-api/Controllers/M2mController.cs`：純以 API Key + HMAC 驗簽，完全沒有使用上述任何方法。
  - `apps/admin-api/Controllers/AuditLogsController.cs`：純唯讀查詢端點，完全沒有使用上述任何方法。
- **改善方向**：
  兩者改為直接繼承 `ControllerBase`，保留 `[ApiController]` 宣告，消除不必要的繼承層次與虛擬相依。

---

## 3. 具體變更清單 (Detailed Changes)

### 3.1 子任務 1：收斂 M2M HMAC 驗簽模組至 Developer API
- **移入檔案**：
  - `apps/developer-api/Security/Hmac/HmacAuthentication.cs`（由 `auth-shared` 搬移並調整命名空間）
  - `apps/developer-api/Security/Hmac/HmacSignature.cs`（由 `auth-shared` 搬移並調整命名空間）
- **移除檔案**：
  - `apps/auth-shared/Hmac/HmacAuthentication.cs`
  - `apps/auth-shared/Hmac/HmacSignature.cs`
- **更新調用端**：
  - `apps/developer-api/Controllers/M2mController.cs`：引用改為 `using DeveloperApi.Security.Hmac;`
  - `apps/developer-api/Program.cs`：引用改為 `using DeveloperApi.Security.Hmac;`
  - `apps/developer-api/Security/ApiKeyHmacResolver.cs`：調整相應引用

### 3.2 子任務 2：Controller 繼承層次瘦身 (Refused Bequest 消除)
- **改動檔案**：
  - `apps/developer-api/Controllers/M2mController.cs`：
    ```csharp
    [Route("api/v1/m2m")]
    [Authorize(AuthenticationSchemes = HmacAuthenticationDefaults.Scheme, Policy = AuthPolicies.M2mProfile)]
    public class M2mController : ControllerBase
    ```
    移除未使用的 `using AuthShared.Web;`。
  - `apps/admin-api/Controllers/AuditLogsController.cs`：
    ```csharp
    [Route("api/v1/admin/audit-logs")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public class AuditLogsController(AdminApiDbContext dbContext) : ControllerBase
    ```
    移除未使用的 `using AuthShared.Web;`。

---

## 4. 驗證與測試接縫 (Verification & Test Seams)

- **單元與整合測試驗收**：
  - 執行 `dotnet test tests/DeveloperApi.Tests`，驗證 M2M HMAC 端點簽章驗證與錯誤處理維持 100% 綠燈。
  - 執行 `dotnet test tests/AdminApi.Tests`，驗證稽核日誌查詢端點維持 100% 綠燈。
  - 執行 `dotnet test AuthPlatform.slnx`，確保全方案 315 項測試無任何回歸破壞。
- **編譯建置驗收**：
  - 執行 `dotnet build AuthPlatform.slnx`，確認零警告零錯誤。

---

## 5. 邊界與 Out of Scope

- **不修改真實跨服務共享模組**：`auth-shared` 中的 `ClientSecrets/`、`ApplicationStatus.cs`、`ClientProperties.cs` 與 `OpenIddictStoreDbContext.cs` 確實服務於多個服務，維持原狀。
- **不變更任何公開 API 契約**：所有 HTTP 端點路徑、路由參數、請求/回應 JSON 格式皆 100% 保持相容，無 Breaking Changes。
