import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e/specs',
  fullyParallel: true,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    launchOptions: { args: ['--host-resolver-rules=MAP *.1111.com.tw 127.0.0.1'] },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
})
