#!/usr/bin/env bash
# 專用於端到端煙霧測試的通訊（Email / SMS）Token 與 OTP 萃取函式庫
# 優先透過 Mailpit / Smspit REST API 查詢攔截內容；若服務未啟動則自動回退到 member-api 容器日誌解析。

MAILPIT_API_URL="${MAILPIT_API_URL:-http://127.0.0.1:8025}"
SMSPIT_API_URL="${SMSPIT_API_URL:-http://127.0.0.1:8026}"

# ==================== Email Token 擷取 ====================

fetch_token_from_mailpit() {
  local to_email="$1"
  local subject="$2"

  local search_res msg_id=""
  search_res=$(curl -sfG "${MAILPIT_API_URL}/api/v1/search" --data-urlencode "query=to:${to_email}" 2>/dev/null) || return 1

  if command -v jq >/dev/null 2>&1; then
    msg_id=$(printf '%s' "$search_res" | jq -r --arg subj "$subject" '.messages[]? | select(.Subject | contains($subj)) | .ID' 2>/dev/null | head -1)
  elif command -v python3 >/dev/null 2>&1; then
    msg_id=$(python3 -c '
import sys, json
try:
    data = json.loads(sys.argv[1])
    subj = sys.argv[2]
    for m in data.get("messages", []):
        if subj in m.get("Subject", ""):
            print(m.get("ID", ""))
            break
except Exception:
    pass
' "$search_res" "$subject" 2>/dev/null)
  fi

  if [ -n "$msg_id" ] && [ "$msg_id" != "null" ]; then
    local msg_content token
    msg_content=$(curl -sf "${MAILPIT_API_URL}/api/v1/message/${msg_id}" 2>/dev/null) || return 1
    token=$(printf '%s' "$msg_content" | grep -oP 'token=\K[A-Za-z0-9]+' | head -1)
    if [ -n "$token" ]; then
      echo "$token"
      return 0
    fi
  fi
  return 1
}

fetch_token_from_logs() {
  local to_email="$1"
  local subject="$2"
  local needle="寄送信件至 ${to_email}：${subject}"
  docker compose logs member-api --no-log-prefix 2>/dev/null | grep -A1 -F "$needle" | grep -oP 'token=\K[^&\s]+' | tail -1
}

mail_token() {
  local to_email="$1"
  local subject="$2"
  local use_mailpit=0

  if curl -sf "${MAILPIT_API_URL}/api/v1/messages" >/dev/null 2>&1; then
    use_mailpit=1
  fi

  for _ in $(seq 1 40); do
    local found=""
    if [ "$use_mailpit" -eq 1 ]; then
      found=$(fetch_token_from_mailpit "$to_email" "$subject" || true)
    else
      found=$(fetch_token_from_logs "$to_email" "$subject" || true)
    fi

    if [ -n "$found" ]; then
      echo "$found"
      return 0
    fi
    sleep 0.5
  done

  return 1
}

get_mail_token() {
  mail_token "$@"
}

wait_for_token() {
  mail_token "$@"
}

# ==================== SMS OTP 擷取 ====================

fetch_sms_from_smspit() {
  local phone="$1"
  local res
  res=$(curl -sfG "${SMSPIT_API_URL}/api/v1/messages" --data-urlencode "to=${phone}" 2>/dev/null) || return 1

  local message_text=""
  if command -v jq >/dev/null 2>&1; then
    message_text=$(printf '%s' "$res" | jq -r '.messages[0]?.message // empty' 2>/dev/null)
  elif command -v python3 >/dev/null 2>&1; then
    message_text=$(python3 -c '
import sys, json
try:
    data = json.loads(sys.argv[1])
    msgs = data.get("messages", [])
    if msgs:
        print(msgs[0].get("message", ""))
except Exception:
    pass
' "$res" 2>/dev/null)
  fi

  if [ -n "$message_text" ]; then
    # 提取 6 碼數字 OTP
    local otp
    otp=$(printf '%s' "$message_text" | grep -oP '\b\d{6}\b' | head -1)
    if [ -n "$otp" ]; then
      echo "$otp"
      return 0
    fi
  fi
  return 1
}

fetch_sms_from_logs() {
  local phone="$1"
  local needle="寄送簡訊至 ${phone}"
  docker compose logs member-api --no-log-prefix 2>/dev/null | grep -F "$needle" | grep -oP '\b\d{6}\b' | tail -1
}

sms_otp() {
  local phone="$1"
  local use_smspit=0

  if curl -sf "${SMSPIT_API_URL}/api/v1/messages" >/dev/null 2>&1; then
    use_smspit=1
  fi

  for _ in $(seq 1 40); do
    local found=""
    if [ "$use_smspit" -eq 1 ]; then
      found=$(fetch_sms_from_smspit "$phone" || true)
    else
      found=$(fetch_sms_from_logs "$phone" || true)
    fi

    if [ -n "$found" ]; then
      echo "$found"
      return 0
    fi
    sleep 0.5
  done

  return 1
}

get_sms_otp() {
  sms_otp "$@"
}
