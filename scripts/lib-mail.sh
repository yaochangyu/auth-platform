#!/usr/bin/env bash
# 專用於端到端煙霧測試的郵件 Token 萃取函式庫
# 優先透過 Mailpit REST API 查詢真實 SMTP 攔截信件；若 Mailpit 未啟動則自動回退到 member-api 容器日誌解析。

MAILPIT_API_URL="${MAILPIT_API_URL:-http://127.0.0.1:8025}"

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

# 相容別名
get_mail_token() {
  mail_token "$@"
}

wait_for_token() {
  mail_token "$@"
}
