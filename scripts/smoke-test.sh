#!/usr/bin/env bash
# 端到端煙霧測試：註冊 -> 啟用 -> 登入 -> 變更密碼 -> 忘記密碼重設 -> 授權撤銷
# 前置：docker compose up -d 已啟動，且 /etc/hosts 已將 member.1111.com.tw 指向 127.0.0.1
set -euo pipefail

BASE="http://member.1111.com.tw:8080"
JAR=$(mktemp)
trap 'rm -f "$JAR"' EXIT

EMAIL="smoke-$(date +%s)@1111.com.tw"
PASSWORD="P@ssw0rd2026!"
NEW_PASSWORD="P@ssw0rd2027!"

step() { echo "== $1 =="; }
wait_for_token() {
  # 從 member-api 容器日誌撈出寄給指定 Email 的最新驗證連結 token（以「收件人：主旨」整段比對，
  # 避免同一 Email 先後收到驗證信與重設密碼信時互相誤判）。
  # LoggingEmailSender 一則訊息輸出「收件人/主旨」與「內文連結」分屬相鄰兩行，故用 -A1 一併取得；
  # Outbox 由背景 Worker 非同步寄送，容器日誌落地可能略晚於 HTTP 回應，故重試等待。
  local needle="寄送信件至 $1：$2"
  for _ in $(seq 1 20); do
    local found
    found=$(docker compose logs member-api --no-log-prefix | grep -A1 -F "$needle" | grep -oP 'token=\K[^&\s]+' | tail -1)
    if [ -n "$found" ]; then
      echo "$found"
      return 0
    fi
    sleep 0.5
  done
  echo "找不到寄給 $1 的「$2」連結" >&2
  return 1
}

step "1. 註冊"
curl -sf -c "$JAR" -X POST "$BASE/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\",\"confirmPassword\":\"$PASSWORD\",\"displayName\":\"Smoke Test\"}"
echo

step "2. 啟用信箱"
TOKEN=$(wait_for_token "$EMAIL" "請驗證您的會員信箱")
curl -sf -c "$JAR" -X POST "$BASE/api/v1/auth/verify-email" \
  -H "Content-Type: application/json" \
  -d "{\"verificationToken\":\"$TOKEN\"}"
echo

step "3. 登入"
curl -sf -c "$JAR" -b "$JAR" -X POST "$BASE/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}"
echo

step "4. 取得個人檔案（驗證 Session Cookie 有效）"
curl -sf -b "$JAR" "$BASE/api/v1/member/profile"
echo

step "5. 變更密碼"
curl -sf -c "$JAR" -b "$JAR" -X PUT "$BASE/api/v1/member/password" \
  -H "Content-Type: application/json" \
  -d "{\"currentPassword\":\"$PASSWORD\",\"newPassword\":\"$NEW_PASSWORD\",\"confirmPassword\":\"$NEW_PASSWORD\"}"
echo

step "6. 忘記密碼"
curl -sf -X POST "$BASE/api/v1/auth/forgot-password" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\"}"
echo

RESET_TOKEN=$(wait_for_token "$EMAIL" "重設您的密碼")
step "7. 重設密碼"
curl -sf -X POST "$BASE/api/v1/auth/reset-password" \
  -H "Content-Type: application/json" \
  -d "{\"verificationToken\":\"$RESET_TOKEN\",\"newPassword\":\"$PASSWORD\",\"confirmPassword\":\"$PASSWORD\"}"
echo

step "8. 以重設後密碼重新登入"
curl -sf -c "$JAR" -b "$JAR" -X POST "$BASE/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}"
echo

step "9. 查詢已連結應用程式清單（預期為空清單）"
curl -sf -b "$JAR" "$BASE/api/v1/member/connected-apps"
echo

step "全部流程通過"
