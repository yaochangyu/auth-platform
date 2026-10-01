// 輪詢直到 fn 回傳非 undefined，逾時則拋錯
export async function poll<T>(fn: () => Promise<T | undefined>, what: string, timeoutMs = 15_000): Promise<T> {
  const end = Date.now() + timeoutMs
  while (Date.now() < end) {
    const v = await fn()
    if (v !== undefined) return v
    await new Promise((r) => setTimeout(r, 500))
  }
  throw new Error(`逾時未取得 ${what}`)
}
