# CLAUDE.md

此檔案為 AI 助理在此專案（`auth-platform`）中協作的指導規範。  
所有回覆使用台灣用語的繁體中文，簡潔明瞭。

---

## 專案專屬開發規範（強制遵守）

### 1. 技能呼叫規範
- **實作代碼**：實作程式碼時**必須使用 `/ponytail` skill**（以最簡約、最短、最少依賴且真正可行的最優解實作，拒絕過度設計與非必要樣板）。
- **審核代碼**：審核代碼時**必須使用 `/code-review` skill**（雙軸審查：規格 Spec 與代碼標準 Standards）。
- **探索需求**：探索需求時**必須使用 `/grill-with-doc` skill**，透過嚴格盤問與多面向探討完善規格。
- **規格生成 (`/to-spec`)**：產出 Spec 時，除了發布至 GitHub Issue Tracker（帶 `ready-for-agent` 標籤）外，**必須強制在本地端 `docs/specs/{spec-name}.md` 同步存檔一份**並納入 Git 版本控管。
- **工單拆解 (`/to-tickets`)**：工單發布至 GitHub Issue Tracker（帶 `ready-for-agent` 標籤與 blocking edges），本地鏡像檔依需求記錄。

### 2. 測試開發規範
- **API 測試範疇**：後端 API 的測試**只需要 BDD + WebApplicationFactory + Testcontainers**，以真實 PostgreSQL 容器進行情境整合測試，不撰寫非必要的孤立單元測試。
- **強制審核點（人機確認）**：**測試案例（BDD Scenarios）編寫完成後，必須先讓使用者檢視並確認審核**，確認無誤後方可開始進入程式碼實作階段。

### 3. 後端開發規範 (ASP.NET Core)
- 參考標準範本：[`yaochangyu/api.template`](https://github.com/yaochangyu/api.template)。
- **執行環境**：.NET 10 (LTS)，目標 Framework 為 `net10.0`。
- **開發模式**：全專案嚴格採用 **API First**，先撰寫 OpenAPI 規格文件，由契約驅動前後端實作。
- **架構風格**：採用 Controller 架構 + Clean Architecture / Service-Repository。
- **資料庫**：PostgreSQL，搭配 EF Core 與 Migration 機制。
- **身分驗證**：HttpOnly Cookie Authentication（主網域 `.1111.com.tw` 共享）。

### 4. 前端開發規範 (Vue 3)
- 前端專案位於 `apps/member-web/`。
- **強制套用 `vue-best-practices` 技能規範**：
  - Composition API + `<script setup lang="ts">`。
  - SFC 結構順序維持 `<script>` → `<template>` → `<style>`。
  - 路由 View 維持薄視圖（Thin Views / Composition Surface），業務 UI 模組化拆為子組件。
  - 狀態與副作用統一抽離至 Composables（如 `useAuth`, `usePasswordReset`, `useVerification`）。
  - 明確 Props/Emits 型別契約，保持最小原始狀態。

---

## 核心互動原則

1. **強制確認**：凡涉及架構重大分支或未定決策，使用 `ask_question` 工具進行結構化詢問，不得擅自假設。
2. **計畫書生命週期**：
   - 檔名格式：`{project}-plan.md`，開頭標記時間與進度。
   - 執行時即時更新核取方塊 `- [x]`。
   - 完成後封存至 `.archive/`。

---

## Agent skills

### Issue tracker

GitHub issues tracked via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Domain docs

Single-context documentation layout (`CONTEXT.md` and `docs/adr/` at repo root). See `docs/agents/domain.md`.
