import type { Page } from '@playwright/test'

const BASE = 'http://member.1111.com.tw:8080'

export class MemberPage {
  constructor(readonly page: Page) {}

  gotoRegister() { return this.page.goto(`${BASE}/register`) }
  gotoVerifyEmail(token: string) { return this.page.goto(`${BASE}/verify-email?token=${token}`) }
  gotoLogin() { return this.page.goto(`${BASE}/login`) }
  gotoProfile() { return this.page.goto(`${BASE}/member`) }

  async fillRegisterForm(f: { email: string; displayName: string; password: string }) {
    await this.page.locator('#email').fill(f.email)
    await this.page.locator('#displayName').fill(f.displayName)
    await this.page.locator('#password').fill(f.password)
    await this.page.locator('#confirmPassword').fill(f.password)
  }
  submitRegister() { return this.page.getByRole('button', { name: '註冊' }).click() }

  /** account 可為 Email 或手機號碼 */
  async fillLoginForm(account: string, password: string) {
    await this.page.locator('#email').fill(account)
    await this.page.locator('#password').fill(password)
  }
  submitLogin() { return this.page.getByRole('button', { name: '登入' }).click() }

  async getProfileData() {
    const row = (label: string) =>
      this.page.locator('dl > div', { hasText: label }).locator('dd').innerText()
    return { email: await row('Email'), displayName: await row('暱稱'), status: await row('狀態') }
  }
  clickLogout() { return this.page.getByRole('button', { name: '登出' }).click() }

  /** 以頁面同源 fetch 呼叫 API（帶 Cookie；request fixture 吃不到 host-resolver） */
  async api(method: string, path: string, body?: unknown) {
    return this.page.evaluate(
      async ([m, p, b]) => {
        const r = await fetch(p as string, {
          method: m as string,
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: b === undefined ? undefined : JSON.stringify(b),
        })
        return { status: r.status, body: await r.json().catch(() => null) }
      },
      [method, path, body] as const,
    )
  }
}
