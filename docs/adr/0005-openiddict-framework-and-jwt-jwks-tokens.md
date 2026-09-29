# 0005. 採用 OpenIddict 作為 OAuth 2.1 / OIDC 授權伺服器與非對稱 JWT 權杖簽章

- **狀態 (Status)**: 已採納 (accepted)
- **日期 (Date)**: 2026-09-29

## 背景脈絡 (Context)

在實作統一身分與授權平台的授權伺服器模組（`apps/auth-server`）時，系統需支援標準的 OAuth 2.1 授權碼模式（Authorization Code Grant + PKCE）與 OpenID Connect (OIDC) Core 1.0 身分驗證。
我們面臨以下關鍵架構抉擇：
1. **授權框架選型**：採用自研端點、商用 Duende IdentityServer，或開源成熟的 OpenIddict。
2. **權杖簽章與驗證拓元**：採用參考權杖（Reference Token + Introspection）或非對稱加密 JWT（RS256/ES256 + JWKS）。
3. **會話與單點登入 (SSO) 整合**：授權伺服器如何感知自然人會員的既有登入狀態。

## 決策 (Decision)

### 1. 採用 OpenIddict 作為核心授權協議框架
- **開源且無商業授權限制**：遵循 Apache 2.0 授權，避免 Duende 等商業套件在組織擴展時的授權合規風險。
- **標準相容度**：原生支援 OAuth 2.0 / 2.1 與 OIDC 規範，包含強制的 PKCE 驗證、State 比對、重定向 URI 嚴格校驗與 OpenID Discovery (`/.well-known/openid-configuration`)。
- **EF Core 原生整合**：OpenIddict 提供出色的 EF Core 支援，能與現有 PostgreSQL 資料庫架構無縫協同，儲存 Client Applications、Authorizations、Scopes 與 Tokens。

### 2. 採用非對稱加密 JWT 搭配公開 JWKS 端點
- **簽章演算法**：採用 RSA (RS256) 或 ECDSA (ES256) 非對稱金鑰對 Access Token 與 ID Token 進行數位簽章。
- **離線驗證解耦**：授權伺服器對外暴露 `/.well-known/jwks.json` 公開金鑰端點。各業務端資源伺服器（Resource Servers）可快取公開金鑰並本機離線驗證 JWT Claims 與簽章，無需每次呼叫皆回呼授權伺服器進行 Token 內省（Introspection），大幅降低身分核心的運算壓力並提升系統整體吞吐量。
- **權杖時效策略**：Access Token 設定短效生命週期（如 15 分鐘），並搭配 Refresh Token 輪替（Rotation）機制以兼顧安全性與續航體驗。

### 3. 基於主網域 Session Cookie 的無感單點登入 (Silent SSO)
- 授權伺服器（`auth.1111.com.tw`）直接讀取既有會員中心簽發的 `.1111.com.tw` HttpOnly Session Cookie 進行身分鑑別。
- 當會員已在會員中心登入，導向至 `/connect/authorize` 時將自動識別當前 Member 身分；若尚未登入，則導向會員中心登入頁面並攜帶 `returnUrl`，完成登入後回跳接續授權流程。

## 後果與權衡 (Consequences)

- **優點**：
  - 遵循零信任架構，各資源伺服器僅依賴公開 JWKS 驗證 JWT，模組間保持極佳的高內聚與低耦合。
  - 徹底複用既有 Member Center 的會員密碼防爆破、暫時鎖定、Email 驗證與 SecurityStamp 會話作廢防線。
- **權衡與代價**：
  - JWT 一旦簽發，在到期前於資源伺服器端屬於無狀態驗證；若需實現「即時緊急封鎖特定 Access Token」，需輔以黑名單推播或依靠短過期時間（15 分鐘以內）自然衰退。
  - 需妥善管理 Auth Server 的非對稱私鑰存儲與輪替機制（Key Rotation）。
