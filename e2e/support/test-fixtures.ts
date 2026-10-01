import { test as base } from '@playwright/test'
import { MailpitClient } from './mailpit-client'
import { SmspitClient } from './smspit-client'

export const test = base.extend<{ mailpit: MailpitClient; smspit: SmspitClient }>({
  mailpit: async ({}, use) => use(new MailpitClient()),
  smspit: async ({}, use) => use(new SmspitClient()),
})
export { expect } from '@playwright/test'
