import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e/specs',
  fullyParallel: true,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP *.1111.com.tw 127.0.0.1',
        // PKCE 需要 crypto.subtle，僅限 secure context；本機 http 網域須明確視為安全來源
        '--unsafely-treat-insecure-origin-as-secure=http://developer.1111.com.tw:8092,http://admin.1111.com.tw:8093',
      ],
    },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium', channel: 'chromium' } }],
})
