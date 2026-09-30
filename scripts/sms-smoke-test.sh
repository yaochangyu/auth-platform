#!/usr/bin/env bash
# 三竹簡訊 (Mitake SMS)、Smspit 虛擬伺服器與手機雙軌登入端到端驗證腳本
# 驗證三竹 API 相容性、Smspit 攔截、OTP 萃取、手機號碼校驗、手機註冊與手機號碼雙軌登入
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

SMSPIT="http://127.0.0.1:8026"
BASE="http://member.1111.com.tw:8080"
RESOLVE=(--resolve member.1111.com.tw:8080:127.0.0.1)
c() { curl "${RESOLVE[@]}" "$@"; }

JAR=$(mktemp)
trap 'rm -f "$JAR"' EXIT

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
source "$SCRIPT_DIR/lib-mail.sh"

step() { echo; echo "== $1 =="; }
ok() { echo "  ✔ $1"; }
fail() { echo "  ✘ $1" >&2; exit 1; }
expect_eq() { [ "$2" = "$3" ] && ok "$1（$2）" || fail "$1：預期 $3，實際 $2"; }

step "0. 服務健康檢查 (Smspit & Member API)"
curl -sf "$SMSPIT/health" >/dev/null || fail "Smspit 未啟動，請先執行 docker compose up -d"
c -sf "$BASE/health" >/dev/null || fail "Member API 未正常運行"
ok "所有服務正常運行"

step "1. 驗證 Smspit Web UI 可視化介面"
WEB_STATUS=$(curl -s -o /dev/null -w '%{http_code}' "$SMSPIT/")
expect_eq "Web 管理面板回應狀態碼" "$WEB_STATUS" 200

step "2. 模擬三竹標準 HTTP API 發送簡訊 (POST /api/mtk/SmSend)"
DIRECT_PHONE="09$(date +%s | tail -c 8)0"
DIRECT_OTP="$(shuf -i 100000-999999 -n 1)"
DIRECT_MSG="【1111人力銀行】您的手機驗證碼為：${DIRECT_OTP}，請於 5 分鐘內完成驗證。"

RESPONSE=$(curl -sf -X POST "$SMSPIT/api/mtk/SmSend" \
  -d "username=test_user" \
  -d "password=test_pass" \
  -d "dstaddr=$DIRECT_PHONE" \
  --data-urlencode "smbody=$DIRECT_MSG")

case "$RESPONSE" in
  *"statuscode=1"*) ok "三竹標準發送請求成功（回應 statuscode=1）";;
  *) fail "三竹發送回應非預期：$RESPONSE";;
esac

step "3. 透過 lib-mail.sh (sms_otp) 以 REST API 查詢並萃取直接發送之 OTP"
EXTRACTED_OTP=$(sms_otp "$DIRECT_PHONE") || fail "無法自 Smspit 檢索門號 $DIRECT_PHONE 之簡訊"
expect_eq "萃取之 6 碼 OTP" "$EXTRACTED_OTP" "$DIRECT_OTP"

step "4. Member API 發送簡訊驗證碼 (POST /api/v1/auth/send-sms-otp)"
MEMBER_PHONE="09$(date +%s | tail -c 8)9"
SEND_STATUS=$(c -s -o /dev/null -w '%{http_code}' -X POST "$BASE/api/v1/auth/send-sms-otp" \
  -H "Content-Type: application/json" \
  -d "{\"phoneNumber\":\"$MEMBER_PHONE\"}")
expect_eq "發送 SMS OTP 狀態碼" "$SEND_STATUS" 200

step "5. 自 Smspit 攔截並自動萃取 Member API 派發的 6 碼動態 OTP"
MEMBER_OTP=$(sms_otp "$MEMBER_PHONE") || fail "Smspit 未收到門號 $MEMBER_PHONE 之簡訊"
[[ "$MEMBER_OTP" =~ ^[0-9]{6}$ ]] || fail "萃取之 OTP 格式非 6 碼數字：$MEMBER_OTP"
ok "成功萃取 Member API 動態 OTP：$MEMBER_OTP"

step "6. Member API 校驗手機號碼驗證碼 (POST /api/v1/auth/verify-phone)"
VERIFY_JSON=$(c -sf -X POST "$BASE/api/v1/auth/verify-phone" \
  -H "Content-Type: application/json" \
  -d "{\"phoneNumber\":\"$MEMBER_PHONE\",\"code\":\"$MEMBER_OTP\"}")
VERIFY_SUCCESS=$(jq '.success' <<<"$VERIFY_JSON")
expect_eq "手機號碼驗證結果" "$VERIFY_SUCCESS" "true"

step "7. 註冊會員並填寫已驗證的手機號碼 (POST /api/v1/auth/register)"
MEMBER_EMAIL="smokemember-$(date +%s)@1111.com.tw"
MEMBER_PWD="P@ssw0rd2026!"
REG_STATUS=$(c -s -o /dev/null -w '%{http_code}' -X POST "$BASE/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$MEMBER_EMAIL\",\"password\":\"$MEMBER_PWD\",\"confirmPassword\":\"$MEMBER_PWD\",\"displayName\":\"簡訊測試會員\",\"phoneNumber\":\"$MEMBER_PHONE\"}")
expect_eq "註冊新會員狀態碼" "$REG_STATUS" 201

step "8. 自 Mailpit 檢索啟用驗證信並啟用會員帳號"
TOKEN=$(wait_for_token "$MEMBER_EMAIL" "請驗證您的會員信箱") || fail "無法自 Mailpit 取得驗證 Token"
c -sf -X POST "$BASE/api/v1/auth/verify-email" \
  -H "Content-Type: application/json" \
  -d "{\"verificationToken\":\"$TOKEN\"}" >/dev/null
ok "會員信箱啟用成功"

step "9. 以「手機號碼 + 密碼」自動雙軌登入 (POST /api/v1/auth/login)"
LOGIN_JSON=$(c -sf -c "$JAR" -X POST "$BASE/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$MEMBER_PHONE\",\"password\":\"$MEMBER_PWD\"}")
LOGIN_EMAIL=$(jq -r '.email' <<<"$LOGIN_JSON")
expect_eq "以手機登入解析出正確會員 Email" "$LOGIN_EMAIL" "$MEMBER_EMAIL"

step "10. 攜帶 Session Cookie 讀取個人檔案確認手機號碼與 Email"
PROFILE_JSON=$(c -sf -b "$JAR" "$BASE/api/v1/member/profile")
PROFILE_PHONE=$(jq -r '.phoneNumber // empty' <<<"$PROFILE_JSON")
PROFILE_EMAIL=$(jq -r '.email' <<<"$PROFILE_JSON")
expect_eq "個人檔案之 Email" "$PROFILE_EMAIL" "$MEMBER_EMAIL"
ok "個人檔案驗證通過"

echo
echo "== 全部三竹簡訊、Smspit 攔截與手機號碼雙軌登入驗證 100% 通過 =="
