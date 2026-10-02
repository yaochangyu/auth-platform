import type { Page } from '@playwright/test'

const BASE = 'http://developer.1111.com.tw:8092'

export interface AppData { name: string; description: string; contactEmail: string }

export class DeveloperPage {
  constructor(readonly page: Page) {}

  gotoApps() { return this.page.goto(`${BASE}/apps`) }
  gotoNewApp() { return this.page.goto(`${BASE}/apps/new`) }
  gotoAppDetail(id: string) { return this.page.goto(`${BASE}/apps/${id}`) }
  gotoOAuth(id: string) { return this.page.goto(`${BASE}/apps/${id}/oauth`) }
  gotoApiKeys(id: string) { return this.page.goto(`${BASE}/apps/${id}/api-keys`) }

  /** AppHeader 顯示的身分文字（email） */
  getNavIdentity() { return this.page.locator('header nav span').innerText() }

  async fillAppForm(d: AppData) {
    await this.page.locator('#name').fill(d.name)
    await this.page.locator('#description').fill(d.description)
    await this.page.locator('#contactEmail').fill(d.contactEmail)
  }
  submitAppForm() { return this.page.getByRole('button', { name: '建立', exact: true }).click() }

  async fillOAuthForm(d: { redirectUri: string; clientType?: 'Public' | 'Confidential'; scopes?: string[] }) {
    if (d.clientType) await this.page.locator(d.clientType === 'Confidential' ? '#type-confidential' : '#type-public').check()
    await this.page.locator('#redirectUris').fill(d.redirectUri)
    for (const s of d.scopes ?? ['openid']) await this.page.locator(`#scope-${s}`).check() // 儲存至少需一個範疇
  }
  submitOAuthForm() { return this.page.getByRole('button', { name: '儲存 OAuth 設定' }).click() }

  private alert() { return this.page.getByRole('alert') }
  issueSecret() { return this.page.getByRole('button', { name: '產生新 Secret' }).click() }
  getIssuedSecretText() { return this.alert().locator('code').innerText() }
  copySecret() { return this.alert().getByRole('button', { name: '一鍵複製' }).click() }
  dismissSecret() { return this.alert().getByRole('button', { name: '我已保存，關閉' }).click() }

  async fillApiKeyForm(d: { name: string; environment: 'Live' | 'Test'; scopes: string[] }) {
    await this.page.locator('#keyName').fill(d.name)
    await this.page.locator(d.environment === 'Test' ? '#env-test' : '#env-live').check()
    for (const s of d.scopes) await this.page.locator(`#key-scope-${s}`).check()
  }
  submitApiKeyForm() { return this.page.getByRole('button', { name: '建立 API Key' }).click() }
  getIssuedApiKeyText() { return this.alert().locator('code').first().innerText() }
  copyApiKey() { return this.alert().getByRole('button', { name: '複製 API Key' }).click() }
  dismissApiKey() { return this.alert().getByRole('button', { name: '我已保存，關閉' }).click() }
}
