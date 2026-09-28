# 0004. OAuth 2.0 瀏覽器重定向機制與 Consent 責任邊界

- **狀態 (Status)**: 已採納 (accepted)
- **日期 (Date)**: 2026-09-28

## 背景脈絡 (Context)

在統一身分與授權平台中，終端會員需透過 OAuth 2.0 / OIDC 進行跨子系統單點登入 (SSO) 以及第三方應用程式的個人資料存取授權。
在規劃身分伺服器 (`identity-server`)、會員中心 (`member-web`) 與外部應用程式之間的互動架構時，面臨兩大核心決策：
1. **Consent（授權同意）之生命週期責任歸屬**：使用者授權同意介面應由認證伺服器負責渲染，或是交由會員中心前台處理？
2. **通訊模式決策（302 重定向 vs API 呼叫）**：身分驗證與授權流程應透過瀏覽器 302 重定向（Redirection），還是由客戶端直接以 API 呼叫傳遞認證憑據？

## 決策 (Decision)

經架構團隊評估安全邊界與標準規範，我們確立以下兩大決策：

### 1. Consent 系統邊界與責任分工
- **執行期授權同意（Grant Consent）**：全權歸屬於身分伺服器 **`identity-server` (`auth.1111.com.tw`)**。
  在 OAuth 2.0 Authorization Code Flow 中，由 `identity-server` 原生渲染 Consent 授權頁面，明確揭露應用程式請求之權限範疇（Scopes，如個人資料、Email 等），由會員於此原生頁面點選同意或拒絕。此階段直接與 Authorization Code 核發權限邊界綁定。
- **事後授權管理與撤銷（Revoke Consent）**：全權歸屬於會員中心前台 **`member-web` (`member.1111.com.tw`)** 之「已連結應用程式（Connected Apps）」模組。
  會員可在已登入狀態下，隨時檢視歷史授權之第三方應用程式清單、授權時間與存取範疇，並可自主主動撤銷特定應用程式的授權存取。

### 2. 採用 302 瀏覽器重定向而非 API 呼叫
客戶端（包含會員中心前台與外部第三方應用）與 `identity-server` 之間必須透過瀏覽器 302 重定向完成認證與授權，嚴格禁止透過 API 轉發憑據：
- **零信任與憑據隔離**：
  避免會員中心或第三方 SPA 經手使用者的登入帳號與密碼憑據，消除中間人竊聽、側錄與憑據洩漏風險，嚴格遵循 RFC 6749 精神與 OAuth 2.1 規範中全面廢除密碼模式（Resource Owner Password Credentials Grant, ROPC）之安全要求。
- **瀏覽器第一方 Cookie 與無感單點登入 (Silent SSO)**：
  `identity-server` 擁有自身作用域之 HttpOnly, SameSite=Lax, Secure 會話 Cookie。透過 302 重定向，瀏覽器會自動附帶此憑證向 `identity-server` 驗證會話；已登入會員完全無感跳轉即可取得授權碼，無需在各子系統重複輸入憑據。
- **安全防禦能力**：
  全面採用 **Authorization Code + PKCE (Proof Key for Code Exchange)** 流程，防止授權碼遭跨站劫持或 Token 被惡意側錄，確保只有發起登入請求的原始客戶端方能以 Code 換取 Token。

## 後果與權衡 (Consequences)

- **架構清晰**：執行期授權交由 `identity-server` 保障標準性與協議安全性；事後稽核與撤銷交由 `member-web` 提供自助式使用者體驗。
- **最高資安標準**：任何外部或內部業務系統的前端 SPA 皆無法觸碰會員憑據，將憑據外洩面縮減至身分核心邊界內。
- **無感體驗**：兼顧跨子網域安全隔絕與全平台流暢的無感單點登入 (Silent SSO)。
- **實作要求**：各業務前端與第三方客戶端必須完整支援 OAuth 2.0 Authorization Code + PKCE 瀏覽器重定向與 Callback 路由處理邏輯。
