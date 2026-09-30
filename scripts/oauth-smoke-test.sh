#!/usr/bin/env bash
# OAuth 全鏈路端到端煙霧測試（Phase 2 驗收）
#
# 註冊 ➔ 登入 ➔ 開發者建立 App 與 Client 設定 ➔ 第三方 OAuth 2.1 授權碼 (PKCE) ➔ Consent ➔ 換票 ➔ UserInfo
# ➔ Client Credentials 與 API Key + HMAC ➔ 管理員緊急斷路 ➔ 驗證授權與 Token 立即失效 ➔ 稽核紀錄 ➔ 取消停用
#
# 前置：docker compose up -d 已啟動且所有服務 healthy。
# 需要：bash、curl（支援 --resolve）、jq、openssl、GNU grep（-P）、docker compose。
# 網域以 curl --resolve 模擬，不需要修改 /etc/hosts；Session Cookie 的 Domain 為 .1111.com.tw，
# 各站僅埠號不同，Cookie 不分埠號，因此與瀏覽器一樣共用同一個 SSO Session。
set -euo pipefail

# docker compose 指令需要在專案根目錄執行，不論從哪裡呼叫都先切過去。
cd "$(dirname "${BASH_SOURCE[0]}")/.."

MEMBER="http://member.1111.com.tw:8080"
AUTH="http://auth.1111.com.tw:8091"
DEV="http://developer.1111.com.tw:8092"
ADMIN="http://admin.1111.com.tw:8093"
RESOLVE=(
  --resolve member.1111.com.tw:8080:127.0.0.1
  --resolve auth.1111.com.tw:8091:127.0.0.1
  --resolve developer.1111.com.tw:8092:127.0.0.1
  --resolve admin.1111.com.tw:8093:127.0.0.1
)

TS="$(date +%s)-$RANDOM"
PASSWORD='P@ssw0rd2026!'
DEV_EMAIL="dev-$TS@1111.com.tw"
USER_EMAIL="user-$TS@1111.com.tw"
ADMIN_EMAIL="admin-$TS@1111.com.tw"
THIRD_PARTY_CALLBACK="https://thirdparty.example.com/callback"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

PASS=0
step() { echo; echo "== $1 =="; }
ok() { PASS=$((PASS + 1)); echo "  ✔ $1"; }
fail() { echo "  ✘ $1" >&2; exit 1; }
expect_eq() { [ "$2" = "$3" ] && ok "$1（$2）" || fail "$1：預期 $3，實際 $2"; }
# expires_in 是剩餘秒數，會因進位差 1 秒，所以以範圍比對。
expect_between() { [ "$2" -ge "$3" ] && [ "$2" -le "$4" ] && ok "$1（$2）" || fail "$1：預期 $3~$4，實際 $2"; }
expect_nonempty() { [ -n "$2" ] && [ "$2" != "null" ] && ok "$1" || fail "$1：值為空"; }

c() { curl -sS "${RESOLVE[@]}" "$@"; }
http_status() { c -o /dev/null -w '%{http_code}' "$@"; }
location_of() { grep -i '^location:' | sed 's/^[^:]*: *//; s/\r$//'; }
urlenc() { jq -rn --arg v "$1" '$v|@uri'; }
b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }
query_param() { printf '%s' "$1" | sed -n "s/.*[?&]$2=\([^&]*\).*/\1/p"; }

new_verifier() { openssl rand -base64 48 | tr '+/' '-_' | tr -d '=\n'; }
challenge_of() { printf %s "$1" | openssl dgst -sha256 -binary | b64url; }

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
source "$SCRIPT_DIR/lib-mail.sh"

register_and_login() { # email displayName jar
  c -f -o /dev/null -X POST "$MEMBER/api/v1/auth/register" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$1\",\"password\":\"$PASSWORD\",\"confirmPassword\":\"$PASSWORD\",\"displayName\":\"$2\"}"
  local token; token=$(mail_token "$1" "請驗證您的會員信箱") || fail "找不到 $1 的驗證信"
  c -f -o /dev/null -X POST "$MEMBER/api/v1/auth/verify-email" -H 'Content-Type: application/json' -d "{\"verificationToken\":\"$token\"}"
  c -f -o /dev/null -c "$3" -X POST "$MEMBER/api/v1/auth/login" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$1\",\"password\":\"$PASSWORD\"}"
}

# 第一方 SPA（Public Client、免同意）以 PKCE 取得 Access Token：與瀏覽器相同，帶著 SSO Cookie 走授權端點。
first_party_token() { # jar clientId redirectUri scope
  local verifier challenge location code
  verifier=$(new_verifier); challenge=$(challenge_of "$verifier")
  location=$(c -o /dev/null -D - -b "$1" "$AUTH/connect/authorize?client_id=$2&response_type=code&redirect_uri=$(urlenc "$3")&scope=$(urlenc "$4")&state=s&code_challenge=$challenge&code_challenge_method=S256" | location_of)
  code=$(query_param "$location" code)
  [ -n "$code" ] || fail "$2 沒有取得授權碼（Location：$location）"
  c -f -X POST "$AUTH/connect/token" -d grant_type=authorization_code -d "code=$code" -d "client_id=$2" \
    -d "redirect_uri=$3" -d "code_verifier=$verifier" | jq -r .access_token
}

token_error() { c -X POST "$AUTH/connect/token" "$@" | jq -r '.error // "none"'; } # 回應中的 OAuth error 代碼

api() { # method url token [json]
  local method="$1" url="$2" token="$3"; shift 3
  if [ $# -gt 0 ]; then
    c -X "$method" "$url" -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "$1"
  elif [ "$method" = POST ]; then
    # 沒有 Body 的 POST 明確送出 Content-Length: 0（瀏覽器的 fetch 也會這樣做），否則經 nginx 會被回 400。
    c -X POST "$url" -H "Authorization: Bearer $token" -H 'Content-Length: 0'
  else
    c -X "$method" "$url" -H "Authorization: Bearer $token"
  fi
}
api_status() { # method url token [json]
  local method="$1" url="$2" token="$3"; shift 3
  if [ $# -gt 0 ]; then
    http_status -X "$method" "$url" -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "$1"
  else
    http_status -X "$method" "$url" -H "Authorization: Bearer $token"
  fi
}

# HMAC 請求簽章：METHOD \n PATH_AND_QUERY \n TIMESTAMP \n SHA256_HEX(BODY)，以 API Secret 做 HMAC-SHA256。
m2m_echo_status() { # apiKey apiSecret
  local ts body bodyhash canonical sig
  ts=$(date +%s); body='{"ping":1}'
  bodyhash=$(printf %s "$body" | openssl dgst -sha256 -hex | awk '{print $NF}')
  canonical=$(printf 'POST\n/api/v1/m2m/echo\n%s\n%s' "$ts" "$bodyhash")
  sig=$(printf %s "$canonical" | openssl dgst -sha256 -hmac "$2" -hex | awk '{print $NF}')
  http_status -X POST "$DEV/api/v1/m2m/echo" -H "X-Api-Key: $1" -H "X-Timestamp: $ts" -H "X-Signature: $sig" -H 'Content-Type: application/json' -d "$body"
}

step "0. 服務健康檢查"
for url in "$MEMBER/health" "$AUTH/health" "$AUTH/.well-known/openid-configuration" "$AUTH/.well-known/jwks.json" "$DEV/" "$ADMIN/"; do
  expect_eq "GET $url" "$(http_status "$url")" 200
done

step "1. 會員註冊 ➔ 啟用信箱 ➔ 登入（開發者、最終使用者、管理員各一位）"
register_and_login "$DEV_EMAIL" "Smoke Developer" "$WORK/dev.jar"
register_and_login "$USER_EMAIL" "Smoke User" "$WORK/user.jar"
register_and_login "$ADMIN_EMAIL" "Smoke Admin" "$WORK/admin.jar"
expect_eq "開發者取得個人檔案（Session Cookie 有效）" "$(http_status -b "$WORK/dev.jar" "$MEMBER/api/v1/member/profile")" 200
# 管理員角色沒有自助升級管道，由營運直接更新資料庫。
docker compose exec -T postgres psql -U member_api -d member_api -qtc "update members set role='admin' where email='$ADMIN_EMAIL'" >/dev/null
ok "已將 $ADMIN_EMAIL 設為平台管理員"

step "2. 登入時帶 returnUrl（回跳授權端點，純 HTTP 環境也接受 .1111.com.tw 網域）"
RETURN_URL="$AUTH/connect/authorize?client_id=member-web-spa&response_type=code"
expect_eq "returnUrl 為 .1111.com.tw 網域" \
  "$(http_status -X POST "$MEMBER/api/v1/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$USER_EMAIL\",\"password\":\"$PASSWORD\",\"returnUrl\":\"$RETURN_URL\"}")" 200
expect_eq "returnUrl 為外部網域時被拒絕" \
  "$(http_status -X POST "$MEMBER/api/v1/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$USER_EMAIL\",\"password\":\"$PASSWORD\",\"returnUrl\":\"http://evil.example.com/steal\"}")" 400

step "3. 開發者（Dogfooding 登入開發者後台）➔ 建立 App 與 OAuth Client 設定"
DEV_TOKEN=$(first_party_token "$WORK/dev.jar" developer-web "http://developer.1111.com.tw:8092/oauth/callback" "openid profile developer_api")
expect_nonempty "開發者取得 developer-web 的 Access Token" "$DEV_TOKEN"
APP=$(api POST "$DEV/api/v1/applications" "$DEV_TOKEN" '{"name":"Smoke 第三方應用","description":"端到端煙霧測試","contactEmail":"smoke@example.com"}')
APP_ID=$(jq -r .id <<<"$APP"); CLIENT_ID=$(jq -r .clientId <<<"$APP")
expect_nonempty "建立應用專案（id）" "$APP_ID"
expect_eq "專案初始狀態" "$(jq -r .status <<<"$APP")" Active
OAUTH=$(api PUT "$DEV/api/v1/applications/$APP_ID/oauth-client" "$DEV_TOKEN" \
  "{\"clientType\":\"Confidential\",\"redirectUris\":[\"$THIRD_PARTY_CALLBACK\"],\"postLogoutRedirectUris\":[],\"scopes\":[\"openid\",\"profile\",\"email\",\"offline_access\"]}")
expect_eq "OAuth Client 類型" "$(jq -r .clientType <<<"$OAUTH")" Confidential
SECRET_JSON=$(api POST "$DEV/api/v1/applications/$APP_ID/oauth-client/secrets" "$DEV_TOKEN")
SECRET=$(jq -r '.secret // empty' <<<"$SECRET_JSON" 2>/dev/null || true)
[ -n "$SECRET" ] && ok "發行 Client Secret（明文只回應一次）" || fail "發行 Client Secret 失敗：$SECRET_JSON"
KEY_JSON=$(api POST "$DEV/api/v1/applications/$APP_ID/api-keys" "$DEV_TOKEN" '{"name":"煙霧測試金鑰","environment":"Test","scopes":["profile"],"expiresAt":null}')
API_KEY=$(jq -r '.apiKey // empty' <<<"$KEY_JSON" 2>/dev/null || true); API_SECRET=$(jq -r '.apiSecret // empty' <<<"$KEY_JSON" 2>/dev/null || true)
[ -n "$API_KEY" ] || fail "發行 API Key 失敗：$KEY_JSON"
expect_nonempty "發行 API Key（ak_test_ 前綴）" "$(printf %s "$API_KEY" | grep -o '^ak_test_' || true)"
expect_eq "API Key + HMAC 簽章呼叫 M2M 端點" "$(m2m_echo_status "$API_KEY" "$API_SECRET")" 200

step "4. 第三方應用發起授權碼 + PKCE ➔ Consent 同意 ➔ 換票 ➔ UserInfo"
VERIFIER=$(new_verifier); CHALLENGE=$(challenge_of "$VERIFIER")
SCOPES="openid profile email offline_access"
LOCATION=$(c -o /dev/null -D - -b "$WORK/user.jar" "$AUTH/connect/authorize?client_id=$CLIENT_ID&response_type=code&redirect_uri=$(urlenc "$THIRD_PARTY_CALLBACK")&scope=$(urlenc "$SCOPES")&state=xyz&code_challenge=$CHALLENGE&code_challenge_method=S256" | location_of)
case "$LOCATION" in "$MEMBER/oauth/consent?consent_id="*) ok "尚未授權過 ➔ 導向會員中心同意頁";; *) fail "預期導向同意頁，實際：$LOCATION";; esac
CONSENT_ID=$(query_param "$LOCATION" consent_id)
DETAILS=$(c -f -b "$WORK/user.jar" "$MEMBER/api/v1/oauth/consent/$CONSENT_ID")
expect_eq "同意頁顯示應用程式名稱" "$(jq -r .applicationName <<<"$DETAILS")" "Smoke 第三方應用"
expect_eq "同意頁列出的範疇" "$(jq -r '[.scopes[].name] | join(" ")' <<<"$DETAILS")" "$SCOPES"
expect_eq "偽造的 consent_id 被拒絕" "$(http_status -b "$WORK/user.jar" "$MEMBER/api/v1/oauth/consent/forged-id")" 400
RESUME=$(c -o /dev/null -D - -b "$WORK/user.jar" -X POST "$MEMBER/api/v1/oauth/consent/$CONSENT_ID" \
  -d decision=approve -d scope=openid -d scope=profile -d scope=email -d scope=offline_access | location_of)
CODE_LOCATION=$(c -o /dev/null -D - -b "$WORK/user.jar" "$RESUME" | location_of)
case "$CODE_LOCATION" in "$THIRD_PARTY_CALLBACK?"*) ok "同意後回到第三方的 redirect_uri";; *) fail "預期回到 redirect_uri，實際：$CODE_LOCATION";; esac
expect_eq "state 原樣帶回" "$(query_param "$CODE_LOCATION" state)" xyz
CODE=$(query_param "$CODE_LOCATION" code)
expect_eq "consent_id 只能使用一次（重送被拒絕）" \
  "$(http_status -b "$WORK/user.jar" -X POST "$MEMBER/api/v1/oauth/consent/$CONSENT_ID" -d decision=approve -d scope=openid)" 400
TOKENS=$(c -f -X POST "$AUTH/connect/token" -d grant_type=authorization_code -d "code=$CODE" -d "client_id=$CLIENT_ID" \
  -d "redirect_uri=$THIRD_PARTY_CALLBACK" -d "code_verifier=$VERIFIER" -d "client_secret=$SECRET" 2>&1) || true
[ -n "$(jq -r '.access_token // empty' <<<"$TOKENS")" ] || fail "以正確的 code_verifier 與 client_secret 換票失敗：$TOKENS"
ACCESS_TOKEN=$(jq -r .access_token <<<"$TOKENS"); REFRESH_TOKEN=$(jq -r .refresh_token <<<"$TOKENS")
expect_nonempty "取得 Access Token" "$ACCESS_TOKEN"
expect_nonempty "取得 ID Token" "$(jq -r .id_token <<<"$TOKENS")"
expect_nonempty "取得 Refresh Token（有 offline_access）" "$REFRESH_TOKEN"
expect_between "Access Token 效期（15 分鐘，秒）" "$(jq -r .expires_in <<<"$TOKENS")" 895 900
USERINFO=$(c -f "$AUTH/connect/userinfo" -H "Authorization: Bearer $ACCESS_TOKEN")
expect_eq "UserInfo email" "$(jq -r .email <<<"$USERINFO")" "$USER_EMAIL"
expect_eq "UserInfo nickname" "$(jq -r .nickname <<<"$USERINFO")" "Smoke User"
expect_eq "UserInfo email_verified" "$(jq -r .email_verified <<<"$USERINFO")" true
expect_nonempty "UserInfo sub" "$(jq -r .sub <<<"$USERINFO")"
ROTATED=$(c -f -X POST "$AUTH/connect/token" -d grant_type=refresh_token -d "refresh_token=$REFRESH_TOKEN" -d "client_id=$CLIENT_ID" -d "client_secret=$SECRET")
REFRESH_TOKEN=$(jq -r .refresh_token <<<"$ROTATED")
expect_nonempty "斷路前 Refresh Token 續約成功並輪替出新的 Refresh Token（正向對照）" "$REFRESH_TOKEN"
LOCATION=$(c -o /dev/null -D - -b "$WORK/user.jar" "$AUTH/connect/authorize?client_id=$CLIENT_ID&response_type=code&redirect_uri=$(urlenc "$THIRD_PARTY_CALLBACK")&scope=$(urlenc "$SCOPES")&state=again&code_challenge=$CHALLENGE&code_challenge_method=S256" | location_of)
case "$LOCATION" in "$THIRD_PARTY_CALLBACK?code="*) ok "已同意過 ➔ 再次授權不再詢問，直接核發授權碼";; *) fail "預期直接核發授權碼，實際：$LOCATION";; esac

step "5. Client Credentials（後端服務以 client_id + client_secret 換 M2M Token）"
M2M=$(c -f -X POST "$AUTH/connect/token" -d grant_type=client_credentials -d "client_id=$CLIENT_ID" -d "client_secret=$SECRET" -d scope=profile)
expect_between "M2M Access Token 效期（15 分鐘，秒）" "$(jq -r .expires_in <<<"$M2M")" 895 900
expect_eq "M2M 回應不含 Refresh Token" "$(jq -r '.refresh_token // "none"' <<<"$M2M")" none

step "6. 管理員（Dogfooding 登入管理後台）➔ 全域檢視 ➔ 權限隔離"
ADMIN_TOKEN=$(first_party_token "$WORK/admin.jar" admin-web "http://admin.1111.com.tw:8093/oauth/callback" "openid profile admin_api")
expect_nonempty "管理員取得 admin-web 的 Access Token" "$ADMIN_TOKEN"
LIST=$(api GET "$ADMIN/api/v1/admin/applications?pageSize=100" "$ADMIN_TOKEN")
expect_eq "管理員看得到開發者建立的 App" "$(jq -r --arg id "$APP_ID" '[.items[] | select(.id == $id)] | length' <<<"$LIST")" 1
NONADMIN_TOKEN=$(first_party_token "$WORK/user.jar" admin-web "http://admin.1111.com.tw:8093/oauth/callback" "openid profile admin_api")
expect_eq "一般會員即使拿到 admin-web 的 Token 也是 403" "$(api_status GET "$ADMIN/api/v1/admin/applications" "$NONADMIN_TOKEN")" 403
expect_eq "第三方拿到的會員 Access Token 不能呼叫管理 API" "$(api_status GET "$ADMIN/api/v1/admin/applications" "$ACCESS_TOKEN")" 403
expect_eq "開發者後台的 Token 不能呼叫管理 API" "$(api_status GET "$ADMIN/api/v1/admin/applications" "$DEV_TOKEN")" 403
expect_eq "未帶 Token 為 401" "$(http_status "$ADMIN/api/v1/admin/applications")" 401

step "7. 緊急斷路器：管理員停用 App"
expect_eq "停用必須填寫原因" "$(api_status PUT "$ADMIN/api/v1/admin/applications/$APP_ID/status" "$ADMIN_TOKEN" '{"status":"Suspended"}')" 400
BREAKER=$(api PUT "$ADMIN/api/v1/admin/applications/$APP_ID/status" "$ADMIN_TOKEN" '{"status":"Suspended","reason":"煙霧測試：模擬涉嫌濫用"}')
expect_eq "App 狀態" "$(jq -r .application.status <<<"$BREAKER")" Suspended
[ "$(jq -r .circuitBreaker.revokedApiKeys <<<"$BREAKER")" -ge 1 ] && ok "作廢 API Key 數：$(jq -r .circuitBreaker.revokedApiKeys <<<"$BREAKER")" || fail "沒有作廢 API Key"
[ "$(jq -r .circuitBreaker.revokedAuthorizations <<<"$BREAKER")" -ge 1 ] && ok "作廢授權數：$(jq -r .circuitBreaker.revokedAuthorizations <<<"$BREAKER")" || fail "沒有作廢授權"
[ "$(jq -r .circuitBreaker.revokedTokens <<<"$BREAKER")" -ge 1 ] && ok "作廢 Token 數：$(jq -r .circuitBreaker.revokedTokens <<<"$BREAKER")" || fail "沒有作廢 Token"

step "8. 斷路後：Token、授權與所有憑證立即失效"
expect_eq "既有的 Access Token 呼叫 UserInfo" "$(http_status "$AUTH/connect/userinfo" -H "Authorization: Bearer $ACCESS_TOKEN")" 401
# 以下的 400 同時檢查 OAuth 錯誤碼，避免因為不相關的原因（例如參數錯誤）而誤判通過；上面的步驟已證明同樣的請求在斷路前是成功的。
expect_eq "Refresh Token 續約（400）" \
  "$(http_status -X POST "$AUTH/connect/token" -d grant_type=refresh_token -d "refresh_token=$REFRESH_TOKEN" -d "client_id=$CLIENT_ID" -d "client_secret=$SECRET")" 400
expect_eq "Refresh Token 續約的 OAuth 錯誤碼" \
  "$(token_error -d grant_type=refresh_token -d "refresh_token=$REFRESH_TOKEN" -d "client_id=$CLIENT_ID" -d "client_secret=$SECRET")" invalid_grant
expect_eq "Client Credentials 換票（400）" \
  "$(http_status -X POST "$AUTH/connect/token" -d grant_type=client_credentials -d "client_id=$CLIENT_ID" -d "client_secret=$SECRET" -d scope=profile)" 400
expect_eq "Client Credentials 換票的 OAuth 錯誤碼" \
  "$(token_error -d grant_type=client_credentials -d "client_id=$CLIENT_ID" -d "client_secret=$SECRET" -d scope=profile)" unauthorized_client
expect_eq "API Key + HMAC 呼叫 M2M 端點" "$(m2m_echo_status "$API_KEY" "$API_SECRET")" 401
NEW_VERIFIER=$(new_verifier)
expect_eq "重新發起授權被拒絕（不再導向或核發授權碼）" \
  "$(http_status -b "$WORK/user.jar" "$AUTH/connect/authorize?client_id=$CLIENT_ID&response_type=code&redirect_uri=$(urlenc "$THIRD_PARTY_CALLBACK")&scope=openid&state=s&code_challenge=$(challenge_of "$NEW_VERIFIER")&code_challenge_method=S256")" 400

step "9. 稽核紀錄"
AUDIT=$(api GET "$ADMIN/api/v1/admin/audit-logs?targetId=$APP_ID" "$ADMIN_TOKEN")
expect_eq "稽核紀錄筆數" "$(jq -r .total <<<"$AUDIT")" 1
expect_eq "動作" "$(jq -r '.items[0].action' <<<"$AUDIT")" application.suspend
expect_eq "變更前狀態" "$(jq -r '.items[0].before.status' <<<"$AUDIT")" Active
expect_eq "變更後狀態" "$(jq -r '.items[0].after.status' <<<"$AUDIT")" Suspended
expect_eq "原因" "$(jq -r '.items[0].details.reason' <<<"$AUDIT")" "煙霧測試：模擬涉嫌濫用"
expect_nonempty "客戶端 IP" "$(jq -r '.items[0].clientIp' <<<"$AUDIT" | grep -v '^unknown$' || true)"
expect_nonempty "操作人 MemberId" "$(jq -r '.items[0].actorMemberId' <<<"$AUDIT")"
if DELETE_OUTPUT=$(docker compose exec -T postgres psql -U member_api -d member_api -qtc "delete from audit_logs" 2>&1); then
  fail "資料庫竟然允許刪除稽核紀錄"
fi
# 確認是觸發器擋下的，而不是指令本身失敗（例如容器沒起來）。
grep -q "audit_logs 為不可篡改" <<<"$DELETE_OUTPUT" && ok "資料庫觸發器拒絕刪除稽核紀錄（不可篡改）" || fail "刪除失敗但不是觸發器擋下的：$DELETE_OUTPUT"

step "10. 取消停用：Client 恢復，但已作廢的授權、Token 與 API Key 不會復原"
api PUT "$ADMIN/api/v1/admin/applications/$APP_ID/status" "$ADMIN_TOKEN" '{"status":"Active","reason":"煙霧測試：確認安全，恢復"}' >/dev/null
expect_eq "Client Credentials 換票恢復" \
  "$(http_status -X POST "$AUTH/connect/token" -d grant_type=client_credentials -d "client_id=$CLIENT_ID" -d "client_secret=$SECRET" -d scope=profile)" 200
expect_eq "已作廢的 API Key 不會復原" "$(m2m_echo_status "$API_KEY" "$API_SECRET")" 401
expect_eq "已作廢的 Access Token 不會復原" "$(http_status "$AUTH/connect/userinfo" -H "Authorization: Bearer $ACCESS_TOKEN")" 401

echo
echo "全部通過：共 $PASS 項檢查。"
