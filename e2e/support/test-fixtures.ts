import { test as base } from '@playwright/test'
import { MailpitClient } from './mailpit-client'
import { SmspitClient } from './smspit-client'

export const test = base.extend<{ mailpit: MailpitClient; smspit: SmspitClient }>({
  // member-api 限流依 X-Forwarded-For 第一段分區（nginx 會沿用並附加），每個測試用獨立假 IP 避免互相、重跑時觸發 429
  extraHTTPHeaders: async ({}, use) => {
    const n = () => Math.floor(Math.random() * 254) + 1
    await use({ 'X-Forwarded-For': `10.${n()}.${n()}.${n()}` })
  },
  mailpit: async ({}, use) => use(new MailpitClient()),
  smspit: async ({}, use) => use(new SmspitClient()),
})
export { expect } from '@playwright/test'
