import { test, expect } from '../support/test-fixtures'
import { MemberPage } from '../pages/member-page'
import type { MailpitClient } from '../support/mailpit-client'

import { PASSWORD, newEmail, newPhone, registerAndActivate, login } from '../support/member-flow'

test('Email 註冊 ➔ 啟用 ➔ 登入全鏈路', async ({ page, mailpit }) => {
  const member = new MemberPage(page)
  const email = newEmail()
  await registerAndActivate(member, mailpit, email)
  await login(member, email)

  expect(await member.getProfileData()).toMatchObject({ email, displayName: 'E2E 測試員', status: 'Active' })
  const cookies = await page.context().cookies()
  expect(cookies.some((c) => c.domain.endsWith('.1111.com.tw'))).toBe(true)
})

test('手機 OTP 驗證與手機號碼登入', async ({ page, mailpit, smspit }) => {
  const member = new MemberPage(page)
  const email = newEmail()
  const phone = newPhone()
  await member.gotoLogin() // 取得同源以便呼叫 API
  // 註冊表單無手機欄位，改以 API 帶 PhoneNumber 註冊
  const reg = await member.api('POST', '/api/v1/auth/register', {
    email, password: PASSWORD, confirmPassword: PASSWORD, displayName: 'E2E 測試員', phoneNumber: phone,
  })
  expect(reg.status).toBe(201)
  await member.gotoVerifyEmail(await mailpit.waitForToken(email, '請驗證您的會員信箱'))
  await expect(page.getByText('會員狀態：Active')).toBeVisible()

  await member.gotoLogin()
  expect((await member.api('POST', '/api/v1/auth/send-sms-otp', { phoneNumber: phone })).status).toBe(200)
  const otp = await smspit.waitForOtp(phone)
  expect(otp).toMatch(/^\d{6}$/)
  expect((await member.api('POST', '/api/v1/auth/verify-phone', { phoneNumber: phone, code: otp })).status).toBe(200)

  await login(member, phone)
  expect((await member.getProfileData()).email).toBe(email)
})

test('個人檔案生日 Write-Once', async ({ page, mailpit }) => {
  const member = new MemberPage(page)
  const email = newEmail()
  await registerAndActivate(member, mailpit, email)
  await login(member, email)

  const first = await member.api('PATCH', '/api/v1/member/profile', { birthday: '1995-05-20' })
  expect(first.status).toBe(200)
  const second = await member.api('PATCH', '/api/v1/member/profile', { birthday: '1990-01-01' })
  expect(second.status).toBe(409)
})

test('登出後 Auth Guard 攔截受保護路由', async ({ page, mailpit }) => {
  const member = new MemberPage(page)
  const email = newEmail()
  await registerAndActivate(member, mailpit, email)
  await login(member, email)

  await member.clickLogout()
  await expect(page).toHaveURL(/\/login$/)
  await member.gotoProfile()
  await expect(page).toHaveURL(/\/login\?returnUrl=\/member$/)
})
