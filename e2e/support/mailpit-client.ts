import { poll } from './poll'

export class MailpitClient {
  constructor(private base = 'http://127.0.0.1:8025/api/v1') {}

  /** 依收件者（與可選主旨）輪詢信件，從內文萃取 token=XXXX */
  async waitForToken(to: string, subject?: string, timeoutMs?: number): Promise<string> {
    const query = `to:${to}` + (subject ? ` subject:"${subject}"` : '')
    return poll(async () => {
      const res = await fetch(`${this.base}/search?query=${encodeURIComponent(query)}`)
      const { messages } = (await res.json()) as { messages: { ID: string }[] }
      if (!messages?.length) return undefined
      const msg = (await (await fetch(`${this.base}/message/${messages[0].ID}`)).json()) as { Text: string }
      return /token=([\w-]+)/.exec(msg.Text)?.[1]
    }, `${to} 的信件 token`, timeoutMs)
  }

  async clearMessages(): Promise<void> {
    await fetch(`${this.base}/messages`, { method: 'DELETE' })
  }
}
