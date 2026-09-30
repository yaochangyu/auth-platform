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

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
source "$SCRIPT_DIR/lib-mail.sh"

step() { echo "== $1 =="; }

RESOLVE=(--resolve member.1111.com.tw:8080:127.0.0.1)
c() { curl "${RESOLVE[@]}" "$@"; }

step "1. 註冊"
c -sf -c "$JAR" -X POST "$BASE/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\",\"confirmPassword\":\"$PASSWORD\",\"displayName\":\"Smoke Test\"}"
echo

step "2. 啟用信箱"
TOKEN=$(wait_for_token "$EMAIL" "請驗證您的會員信箱")
c -sf -c "$JAR" -X POST "$BASE/api/v1/auth/verify-email" \
  -H "Content-Type: application/json" \
  -d "{\"verificationToken\":\"$TOKEN\"}"
echo

step "3. 登入"
c -sf -c "$JAR" -b "$JAR" -X POST "$BASE/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}"
echo

step "4. 取得個人檔案（驗證 Session Cookie 有效）"
c -sf -b "$JAR" "$BASE/api/v1/member/profile"
echo

step "5. 變更密碼"
c -sf -c "$JAR" -b "$JAR" -X PUT "$BASE/api/v1/member/password" \
  -H "Content-Type: application/json" \
  -d "{\"currentPassword\":\"$PASSWORD\",\"newPassword\":\"$NEW_PASSWORD\",\"confirmPassword\":\"$NEW_PASSWORD\"}"
echo

step "6. 忘記密碼"
c -sf -X POST "$BASE/api/v1/auth/forgot-password" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\"}"
echo

RESET_TOKEN=$(wait_for_token "$EMAIL" "重設您的密碼")
step "7. 重設密碼"
c -sf -X POST "$BASE/api/v1/auth/reset-password" \
  -H "Content-Type: application/json" \
  -d "{\"verificationToken\":\"$RESET_TOKEN\",\"newPassword\":\"$PASSWORD\",\"confirmPassword\":\"$PASSWORD\"}"
echo

step "8. 以重設後密碼重新登入"
c -sf -c "$JAR" -b "$JAR" -X POST "$BASE/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}"
echo

step "9. 查詢已連結應用程式清單（預期為空清單）"
c -sf -b "$JAR" "$BASE/api/v1/member/connected-apps"
echo

step "全部流程通過"
