# OAuth 全鏈路啟動與驗收手冊（Phase 2）

本文說明如何用 `docker compose` 啟動完整平台（會員中心、授權伺服器、開發者後台、管理後台），並以自動化腳本與手動步驟驗收
「註冊 ➔ 登入 ➔ 開發者建立 App ➔ 第三方 OAuth 2.1 授權碼 (PKCE) ➔ Consent ➔ 換票 ➔ UserInfo ➔ 管理員緊急斷路」全鏈路。

Phase 1 會員中心單獨的驗收見 [`e2e-verification.md`](e2e-verification.md)。

## 1. 服務與埠號

| 服務 | 對外網址（本機） | 說明 | 依賴（healthy 才啟動） |
|---|---|---|---|
| `postgres` | — | 單一資料庫，各服務各自有 DbContext 與獨立的 migration 記錄表（ADR 0006） | — |
| `member-api` | — | 會員註冊、登入、Session Cookie、個人檔案、已連結應用程式 | postgres |
| `member-web` | http://member.1111.com.tw:8080 | 會員中心前端；同時代理 Consent API 到 auth-server | member-api |
| `auth-server` | http://auth.1111.com.tw:8091 | OAuth 2.1 / OIDC 授權伺服器（OpenIddict）：授權、Consent、換票、UserInfo、JWKS | member-api |
| `developer-api` | — | 開發者後台 API：應用專案、OAuth Client 設定、Client Secret、API Key、M2M 連線檢查 | auth-server |
| `developer-web` | http://developer.1111.com.tw:8092 | 開發者後台前端（Dogfooding：以自己的授權伺服器登入） | developer-api |
| `admin-api` | — | 管理後台 API：全平台審核、緊急斷路器、稽核紀錄 | developer-api |
| `admin-web` | http://admin.1111.com.tw:8093 | 管理後台前端 | admin-api |

各後端的 API 只透過對應前端的 nginx 同源代理對外（`/api/`、`/connect/token`），不另外開放埠號。
`auth-server` 需要直接對外：瀏覽器與第三方應用程式都要連 `/connect/authorize`、`/connect/token`、`/connect/userinfo`、JWKS。

> 埠號選擇：授權伺服器用 8091，是因為 8081 常被其他工具占用。埠號寫在 `docker-compose.yml`、`Auth__Issuer` 與前端建置參數，要改的話三處要一起改。

## 2. 啟動

```bash
docker compose build
docker compose up -d
docker compose ps     # 所有後端應為 healthy，四個前端為 running
```

啟動順序由 healthcheck 相依決定：`postgres` ➔ `member-api`（套用 `members` 相關 migration，含 `role` 欄位）➔ `auth-server`（套用 OpenIddict 資料表、寫入種子 Client）
➔ `developer-api`（套用專案與 API Key 資料表）➔ `admin-api`（套用 `audit_logs` 與不可篡改觸發器）➔ 各前端。

### 需要持久化的兩個磁碟區

| 磁碟區 | 掛載位置 | 內容 | 遺失的後果 |
|---|---|---|---|
| `dp-keys` | member-api、auth-server、developer-api 的 `/keys/dp` | ASP.NET Core Data Protection 金鑰環（**三個服務共用**） | member-api 簽發的 Session Cookie 無法被 auth-server 解開（SSO 失效，所有授權請求被導去登入）；developer-api 加密保存的 API Secret 無法解密（所有 API Key 驗證失敗，回 401 並在日誌留下錯誤） |
| `auth-keys` | auth-server 的 `/keys/auth` | JWT 簽章私鑰（`auth-signing-key.pem`）與內部票據加密金鑰 | 重啟後 JWKS 換了新金鑰，已簽發的 Token 全部驗不過 |

> 三個服務必須使用相同的 Application Name（程式內固定為 `auth-platform`）與同一個金鑰目錄，這是 SSO 能成立的前提。

## 3. 本機網域模擬

Session Cookie 的 `Domain` 是 `.1111.com.tw`，所以本機要用該網域的子網域存取。各站只有埠號不同，而 Cookie 不分埠號，因此行為與正式環境相同：在會員中心登入一次，其他站都能無感登入。

- **自動化腳本**：使用 `curl --resolve`，**不需要**修改任何系統設定。
- **瀏覽器手動驗收**：需要 hosts 對應：

```bash
echo "127.0.0.1 member.1111.com.tw auth.1111.com.tw developer.1111.com.tw admin.1111.com.tw" | sudo tee -a /etc/hosts
```

容器環境沒有 TLS 終止，`docker-compose.yml` 對各後端設定 `Auth__RequireHttps=false`：Cookie 改用 `SecurePolicy=SameAsRequest`、OIDC Discovery 允許 HTTP、登入的 `returnUrl` 允許 `.1111.com.tw` 網域的 `http`。
正式環境維持預設值 `true`，這是唯一與正式環境不同的設定差異。

## 4. 自動化驗收：`scripts/oauth-smoke-test.sh`

```bash
bash scripts/oauth-smoke-test.sh
```

需要 `bash`、`curl`（支援 `--resolve`）、`jq`、`openssl`、GNU `grep`（`-P`）與 `docker compose`。腳本每次以時間戳記建立三位新會員，可重複執行；任一檢查失敗立即以非 0 結束。
驗證信由 `member-api` 的 `LoggingEmailSender` 寫入容器日誌（開發用途，未串接真實寄信服務），腳本從日誌撈出連結 token。

腳本完整串接下列閉環（成功時輸出 `全部通過：共 N 項檢查`）：

| 步驟 | 驗證內容 |
|---|---|
| 0 | 各服務健康、OIDC Discovery 與 JWKS 可取得 |
| 1–2 | 三位會員（開發者、最終使用者、管理員）註冊 ➔ 啟用 ➔ 登入；登入帶 `returnUrl`（`.1111.com.tw` 接受、外部網域拒絕） |
| 3 | 開發者以 Dogfooding 登入 developer-web（授權碼 + PKCE）➔ 建立 App ➔ 設定 Confidential Client、Redirect URI、範疇 ➔ 發行 Client Secret 與 API Key ➔ 以 HMAC 簽章呼叫 M2M 端點 |
| 4 | **第三方**發起授權碼 + PKCE ➔ 導向 Consent 同意頁 ➔ 同意頁顯示應用名稱與範疇 ➔ 偽造／重送的 `consent_id` 被拒 ➔ 同意後帶著 `code` 與 `state` 回到 `redirect_uri` ➔ 以 `client_secret` + `code_verifier` 換票 ➔ 取得 Access／ID／Refresh Token ➔ 呼叫 UserInfo（`email`、`nickname`、`email_verified`、`sub`）➔ 再次授權不再詢問 |
| 5 | 後端服務以 `client_credentials` 換 M2M Token（15 分鐘、無 Refresh Token） |
| 6 | 管理員登入 admin-web ➔ 看得到開發者建立的 App；一般會員、第三方的會員 Token、開發者後台的 Token 都不能呼叫管理 API（403），未帶 Token 為 401 |
| 7 | **緊急斷路**：停用必填原因；停用後回報作廢的 API Key、授權、Token 數量 |
| 8 | 斷路後**立即失效**：既有 Access Token 呼叫 UserInfo（401）、Refresh Token 續約（400）、`client_credentials`（400）、API Key + HMAC（401）、重新發起授權被拒 |
| 9 | 稽核紀錄：動作、變更前後狀態、原因、客戶端 IP、操作人；資料庫拒絕刪除稽核紀錄 |
| 10 | 取消停用：Client 恢復（`client_credentials` 又能換票），但已作廢的 API Key 與 Access Token 不會復原 |

## 5. 手動驗收（瀏覽器）

先完成第 3 節的 hosts 設定，再依序操作：

1. **註冊與登入**：開 http://member.1111.com.tw:8080 註冊兩位會員（開發者、使用者）；驗證連結從日誌取得：
   `docker compose logs member-api --no-log-prefix | grep -A1 "請驗證您的會員信箱"`。
2. **指定管理員**：管理員沒有自助升級管道，由營運直接更新資料庫：
   ```bash
   docker compose exec postgres psql -U member_api -d member_api \
     -c "update members set role='admin' where email='管理員的Email'"
   ```
3. **開發者後台**：以開發者身分開 http://developer.1111.com.tw:8092 ➔ 因已有 SSO Session 會無感登入 ➔ 建立應用 ➔ 「OAuth 2.1 設定」選 Confidential、填入 Redirect URI、勾選範疇並儲存 ➔ 產生 Client Secret（只顯示一次，請立即複製）。
4. **第三方授權**：瀏覽器開下列網址（把 `CLIENT_ID` 與 `code_challenge` 換掉；`code_challenge` 可由 `printf %s "$VERIFIER" | openssl dgst -sha256 -binary | openssl base64 -A | tr '+/' '-_' | tr -d '='` 產生）：
   `http://auth.1111.com.tw:8091/connect/authorize?client_id=CLIENT_ID&response_type=code&redirect_uri=<你登記的網址>&scope=openid%20profile%20email&state=x&code_challenge=…&code_challenge_method=S256`
   ➔ 以使用者登入（未登入時會被導到會員中心登入頁，登入後自動回跳）➔ 同意頁按「同意」➔ 瀏覽器被導到你登記的 `redirect_uri?code=…`，從網址取出 `code` 後用 `curl` 換票。
5. **管理員斷路**：以管理員開 http://admin.1111.com.tw:8093 ➔ 應用程式 ➔ 選該應用 ➔ 填原因 ➔ 「停用應用程式（緊急斷路）」➔ 畫面顯示作廢的數量 ➔ 到「稽核紀錄」確認有 `application.suspend`。
   非管理員開同一個網址會看到「此帳號不是平台管理員」。

## 6. BDD 自動化測試回歸

編排不影響既有測試：BDD 測試使用 Testcontainers 各自啟動獨立的 PostgreSQL，與 `docker compose` 環境無關。

```bash
dotnet test AuthPlatform.slnx          # MemberApi 64 + DeveloperApi 70 + AdminApi 34 + AuthServer 77 = 245 項
for app in member-web developer-web admin-web; do (cd apps/$app && npm ci && npm run build); done
```

## 7. 疑難排解

| 現象 | 原因與處理 |
|---|---|
| `port is already allocated` | 埠號被其他程式占用；用 `ss -ltnp` 找出，或改 `docker-compose.yml`（授權伺服器的埠號還要同步改 `Auth__Issuer` 與前端建置參數） |
| 授權請求一直被導回登入 | `dp-keys` 遺失或三個服務沒共用同一個金鑰目錄；`docker compose down -v` 清掉磁碟區後重新啟動 |
| API Key 全部 401，日誌有「無法解密 API Secret」 | Data Protection 金鑰環遺失，既有 API Key 已無法還原，需重新發行 |
| 前端 `invalid_request` / 找不到 redirect_uri | 種子 Client 的回呼網址不含目前的網域與埠號；以 `Auth__SeedRedirectUris__<clientId>__N` 追加（只有 `Auth__RequireHttps=false` 時才生效，正式環境不會採用） |
| 管理後台顯示「不是平台管理員」 | 該會員的 `members.role` 不是 `admin`；更新後需重新換票（重新整理頁面即可，Token 只存在記憶體） |
| 想從頭來過 | `docker compose down -v`（會刪除資料庫與所有金鑰） |

## 8. 已知限制

- **已簽發的 JWT 在到期前仍有效**：Access Token 是自包含 JWT（15 分鐘）。斷路後 auth-server 的 UserInfo 等端點立即拒絕，但只靠 JWKS 離線驗證的第三方資源伺服器要等 Token 自然到期（ADR 0005 已接受的取捨）。
- **稽核紀錄的防篡改止於資料庫觸發器**：擋得住應用程式與一般 SQL，擋不住有 DDL 權限的人；正式環境需靠資料庫帳號權限與外部備份把關。
- **HMAC 簽章在 ±5 分鐘視窗內可重送**：規格只要求時間戳記，沒有 nonce 快取。
- **管理員沒有指派介面**：見第 5 節，需直接更新資料庫。
- **驗證信不會真的寄出**：只寫在 `member-api` 日誌。
