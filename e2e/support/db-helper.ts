import { execFileSync } from 'node:child_process'

// 平台所有服務共用同一個 Postgres（member_api 資料庫／帳號）；以 execFile 傳參數，不經 shell。
const psql = (sql: string) =>
  execFileSync('docker', ['compose', 'exec', '-T', 'postgres', 'psql', '-U', 'member_api', '-d', 'member_api', '-v', 'ON_ERROR_STOP=1', '-c', sql], {
    cwd: process.cwd(), // 由專案根目錄執行 playwright
    stdio: 'pipe',
  })

const GUID = /^[0-9a-f-]{36}$/i
const EMAIL = /^[\w.+-]+@[\w.-]+$/

/** 將會員升級為平台管理員（需在登入／換票之前，role 於換票時寫入 Token） */
export function promoteToAdmin(email: string) {
  if (!EMAIL.test(email)) throw new Error(`非法 email：${email}`)
  psql(`UPDATE members SET role='admin' WHERE email='${email}'`)
}

/** 0: Active, 1: PendingReview, 2: Suspended */
export function setApplicationStatus(appId: string, status: 0 | 1 | 2) {
  if (!GUID.test(appId)) throw new Error(`非法 appId：${appId}`)
  psql(`UPDATE developer_applications SET status=${status} WHERE id='${appId}'`)
}
