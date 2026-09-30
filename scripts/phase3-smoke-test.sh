#!/usr/bin/env bash
# Phase 3 全鏈路端到端煙霧測試（Phase 3 驗收）
#
# 自動化串接驗證：
# 1. 服務健康檢查
# 2. 會員註冊、信箱啟用與 Session Cookie 登入
# 3. 原生 App 公用客戶端 (Public Client) PKCE 授權與深層連結驗證
# 4. 雙軌鑑權 (Dual-Scheme)：Bearer Token 調用 Member Profile 成功
# 5. 客戶端信任分級與帳號治理隔離 (FirstPartyOnly)：Bearer Token 調用密碼變更遭 403 阻擋
# 6. 個人檔案增量補填 (PATCH) 與生日 Write-Once 防弊：首次補填成功、二次覆寫遭 409 拒絕
# 7. 全鏈路權杖撤銷 (RFC 7009)：App 登出撤銷 Refresh Token，再次換票遭 invalid_grant 拒絕
# 8. 公開端點業務層滑動視窗限速 (Rate Limiting)：超限時回傳 429 Too Many Requests 與 Retry-After 標頭
#
# 前置：docker compose up -d 已啟動且所有服務 healthy。
# 需要：bash、curl（支援 --resolve）、jq、openssl、docker compose。
set -euo pipefail

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
DEV_EMAIL="dev-p3-$TS@1111.com.tw"
USER_EMAIL="user-p3-$TS@1111.com.tw"
APP_UNIVERSAL_LINK="https://app.1111.com.tw/oauth/callback"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

PASS=0
step() { echo; echo "== $1 =="; }
ok() { PASS=$((PASS + 1)); echo "  ✔ $1"; }
fail() { echo "  ✘ $1" >&2; exit 1; }
expect_eq() { [ "$2" = "$3" ] && ok "$1（$2）" || fail "$1：預期 $3，實際 $2"; }
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

first_party_token() { # jar clientId redirectUri scope
  local verifier challenge location code
  verifier=$(new_verifier); challenge=$(challenge_of "$verifier")
  location=$(c -o /dev/null -D - -b "$1" "$AUTH/connect/authorize?client_id=$2&response_type=code&redirect_uri=$(urlenc "$3")&scope=$(urlenc "$4")&state=s&code_challenge=$challenge&code_challenge_method=S256" | location_of)
  code=$(query_param "$location" code)
  [ -n "$code" ] || fail "$2 沒有取得授權碼（Location：$location）"
  c -f -X POST "$AUTH/connect/token" -d grant_type=authorization_code -d "code=$code" -d "client_id=$2" \
    -d "redirect_uri=$3" -d "code_verifier=$verifier" | jq -r .access_token
}

api() { # method url token [json]
  local method="$1" url="$2" token="$3"; shift 3
  if [ $# -gt 0 ]; then
    c -X "$method" "$url" -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "$1"
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

step "0. 服務健康檢查"
for url in "$MEMBER/health" "$AUTH/health" "$AUTH/.well-known/openid-configuration" "$AUTH/.well-known/jwks.json" "$DEV/" "$ADMIN/"; do
  expect_eq "GET $url" "$(http_status "$url")" 200
done

step "1. 會員註冊 ➔ 啟用信箱 ➔ 登入"
register_and_login "$DEV_EMAIL" "Phase3 Developer" "$WORK/dev.jar"
register_and_login "$USER_EMAIL" "Phase3 User" "$WORK/user.jar"
expect_eq "使用者以 Session Cookie 取得個人檔案" "$(http_status -b "$WORK/user.jar" "$MEMBER/api/v1/member/profile")" 200

step "2. 開發者建立 Native App 公用客戶端 (Public Client)"
DEV_TOKEN=$(first_party_token "$WORK/dev.jar" developer-web "http://developer.1111.com.tw:8092/oauth/callback" "openid profile developer_api")
expect_nonempty "取得開發者 Access Token" "$DEV_TOKEN"
APP=$(api POST "$DEV/api/v1/applications" "$DEV_TOKEN" '{"name":"Phase3 官方行動 App","description":"原生 App PKCE 測試","contactEmail":"app@example.com"}')
APP_ID=$(jq -r .id <<<"$APP"); CLIENT_ID=$(jq -r .clientId <<<"$APP")
expect_nonempty "建立應用專案（id）" "$APP_ID"

# 宣告為 Public Client，使用 HTTPS Universal Links
OAUTH=$(api PUT "$DEV/api/v1/applications/$APP_ID/oauth-client" "$DEV_TOKEN" \
  "{\"clientType\":\"Public\",\"redirectUris\":[\"$APP_UNIVERSAL_LINK\"],\"postLogoutRedirectUris\":[],\"scopes\":[\"openid\",\"profile\",\"email\",\"offline_access\"]}")
expect_eq "OAuth Client 類型為 Public" "$(jq -r .clientType <<<"$OAUTH")" Public

step "3. 原生 App 執行 PKCE 授權流程並以無 Secret 換票"
VERIFIER=$(new_verifier); CHALLENGE=$(challenge_of "$VERIFIER")
SCOPES="openid profile email offline_access"

# 拒絕 plain 演算法
PLAIN_RES=$(c -i -b "$WORK/user.jar" "$AUTH/connect/authorize?client_id=$CLIENT_ID&response_type=code&redirect_uri=$(urlenc "$APP_UNIVERSAL_LINK")&scope=$(urlenc "$SCOPES")&state=test&code_challenge=$VERIFIER&code_challenge_method=plain" || true)
case "$PLAIN_RES" in *"invalid_request"*) ok "plain 挑戰演算法遭授權端點阻擋";; *) fail "預期阻擋 plain 演算法，實際：$PLAIN_RES";; esac

# S256 挑戰演算法
LOCATION=$(c -o /dev/null -D - -b "$WORK/user.jar" "$AUTH/connect/authorize?client_id=$CLIENT_ID&response_type=code&redirect_uri=$(urlenc "$APP_UNIVERSAL_LINK")&scope=$(urlenc "$SCOPES")&state=pkce_state&code_challenge=$CHALLENGE&code_challenge_method=S256" | location_of)
case "$LOCATION" in "$MEMBER/oauth/consent?consent_id="*) ok "導向會員授權同意頁";; *) fail "預期導向同意頁，實際：$LOCATION";; esac
CONSENT_ID=$(query_param "$LOCATION" consent_id)

RESUME=$(c -o /dev/null -D - -b "$WORK/user.jar" -X POST "$MEMBER/api/v1/oauth/consent/$CONSENT_ID" \
  -d decision=approve -d scope=openid -d scope=profile -d scope=email -d scope=offline_access | location_of)
CODE_LOCATION=$(c -o /dev/null -D - -b "$WORK/user.jar" "$RESUME" | location_of)
case "$CODE_LOCATION" in "$APP_UNIVERSAL_LINK?"*) ok "同意後導回 Universal Links";; *) fail "預期回到 Universal Link，實際：$CODE_LOCATION";; esac
CODE=$(query_param "$CODE_LOCATION" code)

# 公用客戶端不需提供 client_secret 換票
TOKENS=$(c -f -X POST "$AUTH/connect/token" -d grant_type=authorization_code -d "code=$CODE" -d "client_id=$CLIENT_ID" \
  -d "redirect_uri=$APP_UNIVERSAL_LINK" -d "code_verifier=$VERIFIER")
APP_ACCESS_TOKEN=$(jq -r .access_token <<<"$TOKENS")
APP_REFRESH_TOKEN=$(jq -r .refresh_token <<<"$TOKENS")
expect_nonempty "Public Client 換得 Access Token" "$APP_ACCESS_TOKEN"
expect_nonempty "Public Client 換得 Refresh Token" "$APP_REFRESH_TOKEN"

step "4. 雙軌鑑權：以 Bearer Token 存取 Member Profile 端點"
PROFILE=$(api GET "$MEMBER/api/v1/member/profile" "$APP_ACCESS_TOKEN")
expect_eq "Bearer Token 成功讀取 Profile Email" "$(jq -r .email <<<"$PROFILE")" "$USER_EMAIL"
expect_eq "Bearer Token 成功讀取 Profile DisplayName" "$(jq -r .displayName <<<"$PROFILE")" "Phase3 User"
expect_eq "未攜帶 Bearer Token 存取 Profile 回傳 401" "$(http_status "$MEMBER/api/v1/member/profile")" 401

step "5. 客戶端信任分級與帳號治理隔離 (FirstPartyOnly)"
# 第三方 / App Bearer Token 嘗試變更密碼應遭 403 阻擋
CHANGE_PW_STATUS=$(api_status PUT "$MEMBER/api/v1/member/password" "$APP_ACCESS_TOKEN" \
  "{\"currentPassword\":\"$PASSWORD\",\"newPassword\":\"P@ssw0rd2028!\",\"confirmPassword\":\"P@ssw0rd2028!\"}")
expect_eq "Bearer Token 嘗試呼叫 PUT /password 遭 403 阻擋" "$CHANGE_PW_STATUS" 403

# 第三方 / App Bearer Token 嘗試查詢已連結應用清單應遭 403 阻擋
CONNECTED_APPS_STATUS=$(api_status GET "$MEMBER/api/v1/member/connected-apps" "$APP_ACCESS_TOKEN")
expect_eq "Bearer Token 嘗試呼叫 GET /connected-apps 遭 403 阻擋" "$CONNECTED_APPS_STATUS" 403

step "6. 個人檔案增量補填 (PATCH) 與生日 Write-Once 防弊"
# 首次補填生日與自由流動欄位（成功 200）
PATCH_RES=$(api PATCH "$MEMBER/api/v1/member/profile" "$APP_ACCESS_TOKEN" \
  '{"birthday":"1995-05-20","education":"國立臺灣大學 資訊工程研究所","jobTitle":"資深後端架構師","address":"台北市信義區信義路五段7號"}')
expect_eq "首次補填生日成功（1995-05-20）" "$(jq -r .birthday <<<"$PATCH_RES")" "1995-05-20"
expect_eq "更新職稱成功" "$(jq -r .jobTitle <<<"$PATCH_RES")" "資深後端架構師"

# 二次嘗試以不同生日覆寫（拒絕 409 Conflict）
CONFLICT_STATUS=$(api_status PATCH "$MEMBER/api/v1/member/profile" "$APP_ACCESS_TOKEN" '{"birthday":"1996-01-01"}')
expect_eq "已存在生日嘗試二次覆寫不同值遭 409 拒絕" "$CONFLICT_STATUS" 409

# 自由流動欄位允許隨時覆寫更新（成功 200）
PATCH_RES2=$(api PATCH "$MEMBER/api/v1/member/profile" "$APP_ACCESS_TOKEN" '{"jobTitle":"首席安全長"}')
expect_eq "自由流動欄位職稱再次更新成功" "$(jq -r .jobTitle <<<"$PATCH_RES2")" "首席安全長"
expect_eq "既有生日維持原值未被篡改" "$(jq -r .birthday <<<"$PATCH_RES2")" "1995-05-20"

step "7. RFC 7009 權杖撤銷端點與全鏈路登出連動"
# App 登出向授權中心發送 POST /connect/revocation 撤銷 Refresh Token
REVOKE_STATUS=$(c -o /dev/null -w '%{http_code}' -X POST "$AUTH/connect/revocation" \
  -d "token=$APP_REFRESH_TOKEN" -d "client_id=$CLIENT_ID" -d "token_type_hint=refresh_token")
expect_eq "RFC 7009 撤銷端點回傳 200 OK" "$REVOKE_STATUS" 200

# 嘗試以已撤銷之 Refresh Token 再次換票
REVOKED_EXCHANGE=$(c -X POST "$AUTH/connect/token" \
  -d grant_type=refresh_token -d "refresh_token=$APP_REFRESH_TOKEN" -d "client_id=$CLIENT_ID" | jq -r '.error // "none"')
expect_eq "已撤銷 Refresh Token 換票精確回傳 invalid_grant" "$REVOKED_EXCHANGE" "invalid_grant"

step "8. 業務層滑動視窗限速 (Rate Limiting) 與防爆破防護"
RATE_TEST_IP="198.51.100.99"
# 連續快速呼叫忘記密碼端點超出限額（5 次）
for i in $(seq 1 5); do
  c -s -o /dev/null -X POST "$MEMBER/api/v1/auth/forgot-password" \
    -H "X-Forwarded-For: $RATE_TEST_IP" -H 'Content-Type: application/json' \
    -d "{\"email\":\"rate-$i-$TS@1111.com.tw\"}"
done
ok "前置 5 次請求正常送出"

# 第 6 次請求應觸發 429 Too Many Requests
RL_RESP_HEADERS="$WORK/rate_headers.txt"
RL_STATUS=$(c -o /dev/null -w '%{http_code}' -D "$RL_RESP_HEADERS" -X POST "$MEMBER/api/v1/auth/forgot-password" \
  -H "X-Forwarded-For: $RATE_TEST_IP" -H 'Content-Type: application/json' \
  -d "{\"email\":\"rate-flood-$TS@1111.com.tw\"}")
expect_eq "第 6 次頻率超限請求回傳 429 Too Many Requests" "$RL_STATUS" 429

# 驗證帶有 Retry-After 標頭
RETRY_AFTER=$(grep -i '^retry-after:' "$RL_RESP_HEADERS" | awk '{print $2}' | tr -d '\r\n')
expect_nonempty "回應標頭包含 Retry-After（$RETRY_AFTER 秒）" "$RETRY_AFTER"

step "全部 Phase 3 驗收項目通過（通過項目數：$PASS）"
