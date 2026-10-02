import { test, expect } from '../support/test-fixtures'
import { MemberPage } from '../pages/member-page'
import { DeveloperPage } from '../pages/developer-page'
import { newEmail, registerAndActivate, login } from '../support/member-flow'

test.use({ permissions: ['clipboard-read', 'clipboard-write'] })

/** 以會員身分登入（SSO Cookie），再進開發者後台 */
async function signedInDeveloper(page: import('@playwright/test').Page, mailpit: any) {
  const email = newEmail()
  const member = new MemberPage(page)
  await registerAndActivate(member, mailpit, email)
  await login(member, email)
  const dev = new DeveloperPage(page)
  await dev.gotoApps()
  await expect(page).toHaveURL(/developer\.1111\.com\.tw:8092\/apps$/)
  return { dev, email }
}

/** 建立應用並回傳 id */
async function createApp(dev: DeveloperPage, email: string, name = `E2E 應用 ${Date.now()}`) {
  await dev.gotoNewApp()
  await dev.fillAppForm({ name, description: 'E2E 測試用應用', contactEmail: email })
  await dev.submitAppForm()
  await dev.page.waitForURL(/\/apps\/[0-9a-f-]{36}$/)
  return { id: dev.page.url().split('/').pop()!, name }
}

test('SSO 身分延續：免輸入帳密且導覽列顯示會員 Email', async ({ page, mailpit }) => {
  const { dev, email } = await signedInDeveloper(page, mailpit)
  await expect(page.locator('header nav span')).toHaveText(email)
  expect(await dev.getNavIdentity()).toBe(email)
})

test('建立應用程式並於列表顯示 Active', async ({ page, mailpit }) => {
  const { dev, email } = await signedInDeveloper(page, mailpit)
  const { name } = await createApp(dev, email)
  await expect(page.getByText('Active')).toBeVisible()

  await dev.gotoApps()
  const row = page.getByRole('link', { name: new RegExp(name) })
  await expect(row).toBeVisible()
  await expect(row.getByText('Active', { exact: true })).toBeVisible()
})

test('OAuth 設定儲存', async ({ page, mailpit }) => {
  const { dev, email } = await signedInDeveloper(page, mailpit)
  const { id } = await createApp(dev, email)
  await dev.gotoOAuth(id)
  await dev.fillOAuthForm({ redirectUri: 'https://example.com/callback' })
  await page.locator('#scope-openid').check() // 至少需選一個範疇
  await dev.submitOAuthForm()
  await expect(page.getByText('已儲存 OAuth 設定。')).toBeVisible()
})

test('Client Secret 發行、明文警告與複製', async ({ page, mailpit }) => {
  const { dev, email } = await signedInDeveloper(page, mailpit)
  const { id } = await createApp(dev, email)
  await dev.gotoOAuth(id)
  // 預設類型可能為 Public；Secret 面板僅 Confidential 顯示，先切換並儲存
  if (!(await page.getByRole('button', { name: '產生新 Secret' }).isVisible())) {
    await page.locator('#type-confidential').check()
    await dev.fillOAuthForm({ redirectUri: 'https://example.com/callback' })
    await page.locator('#scope-openid').check()
    await dev.submitOAuthForm()
    await expect(page.getByText('已儲存 OAuth 設定。')).toBeVisible()
  }
  await dev.issueSecret()
  await expect(page.getByRole('alert')).toContainText('請立即複製並妥善保存')
  const secret = await dev.getIssuedSecretText()
  expect(secret.length).toBeGreaterThan(10)

  await dev.copySecret()
  await expect(page.getByRole('button', { name: '已複製' })).toBeVisible()
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(secret)

  await dev.dismissSecret()
  await expect(page.getByRole('alert')).toHaveCount(0)
  await expect(page.getByText('使用中', { exact: true })).toBeVisible()
})

test('API Key 發行：ak_test_ 前綴與明文警告', async ({ page, mailpit }) => {
  const { dev, email } = await signedInDeveloper(page, mailpit)
  const { id } = await createApp(dev, email)
  await dev.gotoApiKeys(id)
  const keyName = `E2E key ${Date.now()}`
  // 後端 API Key 允許範疇僅 profile、email，無 developer_api
  await dev.fillApiKeyForm({ name: keyName, environment: 'Test', scopes: ['profile'] })
  await dev.submitApiKeyForm()

  await expect(page.getByRole('alert')).toContainText('請立即複製並妥善保存')
  const apiKey = await dev.getIssuedApiKeyText()
  expect(apiKey.startsWith('ak_test_')).toBe(true)

  await dev.dismissApiKey()
  await expect(page.getByRole('alert')).toHaveCount(0)
  await expect(page.getByText(keyName)).toBeVisible()
})
