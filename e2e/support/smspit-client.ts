import { poll } from './poll'

export class SmspitClient {
  constructor(private base = 'http://127.0.0.1:8026/api/v1') {}

  /** 依手機號碼輪詢簡訊，萃取 6 碼數字 OTP（取最新一筆） */
  async waitForOtp(phone: string, timeoutMs?: number): Promise<string> {
    return poll(async () => {
      const { messages } = (await (await fetch(`${this.base}/messages`)).json()) as {
        messages?: { to: string; message: string; createdAt: string }[]
      }
      const latest = messages
        ?.filter((m) => m.to === phone)
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0]
      return latest && /(?<!\d)(\d{6})(?!\d)/.exec(latest.message)?.[1]
    }, `${phone} 的簡訊 OTP`, timeoutMs)
  }

  async clearMessages(): Promise<void> {
    await fetch(`${this.base}/messages`, { method: 'DELETE' })
  }
}
