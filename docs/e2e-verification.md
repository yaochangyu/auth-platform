# 全流程聯調與容器化驗收

> 本文只涵蓋 Phase 1（會員中心）。完整平台（授權伺服器、開發者後台、管理後台）的啟動與全鏈路驗收見 [`oauth-e2e-verification.md`](oauth-e2e-verification.md)。

## 1. 啟動

```bash
docker compose build
docker compose up -d
```

三個服務：`postgres`（含 healthcheck）、`member-api`（等待 postgres healthy 才啟動，啟動時自動執行 EF Core Migration）、`member-web`（Nginx 靜態檔 + `/api`、`/health` 反向代理至 member-api，等待 member-api healthy 才啟動）。

## 2. 本機網域模擬

Cookie 設定為 `Domain=.1111.com.tw`（見 `apps/member-api/Program.cs` 的 `Auth:CookieDomain`），瀏覽器/curl 只會在請求網域為該值之子網域時才附帶 Cookie。因此需將本機網域指向容器：

```bash
echo "127.0.0.1 member.1111.com.tw" | sudo tee -a /etc/hosts
```

之後以瀏覽器開啟 `http://member.1111.com.tw:8080` 即可完整體驗跨網域 Session Cookie 簽發與攜帶。

容器環境沒有 TLS 終止，`Auth:RequireHttps` 於 `docker-compose.yml` 中設為 `false`，Cookie 改用 `SecurePolicy=SameAsRequest`（純 HTTP 也可寫入），正式環境部署時維持預設 `true`（`SecurePolicy=Always` + 強制 HTTPS 轉址），此為唯一與正式環境不同之設定差異。

## 3. 端到端流程驗證

`scripts/smoke-test.sh` 以 curl 依序執行：註冊 → 啟用信箱 → 登入 → 取得個人檔案 → 變更密碼 → 忘記密碼 → 重設密碼 → 以新密碼重新登入 → 查詢已連結應用程式清單。

驗證信/重設密碼信由 `LoggingEmailSender` 輸出至 `member-api` 容器日誌（開發用途，未串接真實寄信服務），腳本會自動從日誌撈取連結中的 token。

```bash
bash scripts/smoke-test.sh
```

全部步驟皆以 `curl -f` 檢查 HTTP 成功狀態，任一步驟失敗即中止並回傳非 0 結束碼。

## 4. BDD 自動化測試回歸

容器化不影響既有測試（測試使用 Testcontainers 各自啟動獨立的 PostgreSQL，與 docker-compose 環境無關）：

```bash
dotnet test AuthPlatform.slnx
```

預期：`Passed: 64, Failed: 0`。

## 5. 關閉環境

```bash
docker compose down -v
```
