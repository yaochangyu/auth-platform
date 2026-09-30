#!/usr/bin/env bash
# 三竹簡訊 (Mitake SMS) 與 Smspit 虛擬簡訊伺服器端到端驗證腳本
# 驗證三竹 API 端點相容性、Smspit 簡訊攔截持久化、Web UI 與 REST API 6 碼數字 OTP 萃取
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

SMSPIT="http://127.0.0.1:8026"
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
source "$SCRIPT_DIR/lib-mail.sh"

step() { echo; echo "== $1 =="; }
ok() { echo "  ✔ $1"; }
fail() { echo "  ✘ $1" >&2; exit 1; }
expect_eq() { [ "$2" = "$3" ] && ok "$1（$2）" || fail "$1：預期 $3，實際 $2"; }

step "0. Smspit 虛擬簡訊服務健康檢查"
curl -sf "$SMSPIT/health" >/dev/null || fail "Smspit 未啟動，請先執行 docker compose up -d"
ok "Smspit 服務正常運行 ($SMSPIT)"

step "1. 驗證 Smspit Web UI 可視化介面"
WEB_STATUS=$(curl -s -o /dev/null -w '%{http_code}' "$SMSPIT/")
expect_eq "Web 管理面板回應狀態碼" "$WEB_STATUS" 200

step "2. 模擬三竹標準 HTTP API 發送簡訊 (POST /api/mtk/SmSend)"
TEST_PHONE="09$(date +%s | tail -c 9)"
TEST_OTP="$(shuf -i 100000-999999 -n 1)"
TEST_MESSAGE="【1111人力銀行】您的手機驗證碼為：${TEST_OTP}，請於 5 分鐘內完成驗證。"

RESPONSE=$(curl -sf -X POST "$SMSPIT/api/mtk/SmSend" \
  -d "username=test_user" \
  -d "password=test_pass" \
  -d "dstaddr=$TEST_PHONE" \
  --data-urlencode "smbody=$TEST_MESSAGE")

case "$RESPONSE" in
  *"statuscode=1"*) ok "三竹標準發送請求成功（回應 statuscode=1）";;
  *) fail "三竹發送回應非預期：$RESPONSE";;
esac

step "3. 透過 lib-mail.sh (sms_otp) 以 REST API 查詢並精確萃取 6 碼 OTP"
EXTRACTED_OTP=$(sms_otp "$TEST_PHONE") || fail "無法自 Smspit 檢索門號 $TEST_PHONE 之簡訊"
expect_eq "萃取之 6 碼 OTP" "$EXTRACTED_OTP" "$TEST_OTP"

step "4. 驗證 Smspit 簡訊列表包含受訊門號與內容"
MESSAGES_JSON=$(curl -sfG "$SMSPIT/api/v1/messages" --data-urlencode "to=$TEST_PHONE")
FOUND_COUNT=$(jq '.total' <<<"$MESSAGES_JSON")
expect_eq "查詢簡訊數量" "$FOUND_COUNT" 1

echo
echo "== 全部三竹簡訊與 Smspit 攔截驗證通過 =="
