import { test, expect } from '../support/test-fixtures'

test('會員中心首頁渲染登入介面', async ({ page }) => {
  await page.goto('http://member.1111.com.tw:8080/')
  await expect(page).toHaveTitle('會員中心')
  await expect(page.getByRole('heading', { name: '會員登入' })).toBeVisible()
  await expect(page.locator('#email')).toBeVisible()
  await expect(page.locator('#password')).toBeVisible()
})

test('開發者後台首頁渲染', async ({ page }) => {
  await page.goto('http://developer.1111.com.tw:8092/')
  await expect(page).toHaveTitle('開發者後台')
  await expect(page.locator('#app')).not.toBeEmpty()
})

test('管理後台登入頁渲染', async ({ page }) => {
  await page.goto('http://admin.1111.com.tw:8093/')
  await expect(page).toHaveTitle('管理後台')
  await expect(page.locator('#app')).not.toBeEmpty()
})

// 走瀏覽器 page 才吃得到 --host-resolver-rules（request fixture 不會）
test('AuthServer discovery 端點回應 issuer', async ({ page }) => {
  const res = await page.goto('http://auth.1111.com.tw:8091/.well-known/openid-configuration')
  expect(res!.status()).toBe(200)
  expect((await res!.json()).issuer).toBe('http://auth.1111.com.tw:8091/')
})

test('Mailpit 與 Smspit REST API 正常回應', async ({ request, mailpit, smspit }) => {
  expect((await request.get('http://127.0.0.1:8025/api/v1/messages')).ok()).toBe(true)
  expect((await request.get('http://127.0.0.1:8026/api/v1/messages')).ok()).toBe(true)
  expect(mailpit.clearMessages).toBeInstanceOf(Function)
  expect(smspit.clearMessages).toBeInstanceOf(Function)
})
