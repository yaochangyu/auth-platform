import { expect } from '@playwright/test'
import type { MemberPage } from '../pages/member-page'
import type { MailpitClient } from './mailpit-client'

export const PASSWORD = 'Passw0rd!E2e#1'
const uid = () => `${Date.now()}${Math.floor(Math.random() * 1000)}`
export const newEmail = () => `e2e-${uid()}@1111.com.tw`
export const newPhone = () => `09${Math.floor(Math.random() * 1e8).toString().padStart(8, '0')}`

/** 註冊並以信件 Token 啟用，回到登入頁 */
export async function registerAndActivate(member: MemberPage, mailpit: MailpitClient, email: string) {
  await member.gotoRegister()
  await member.fillRegisterForm({ email, displayName: 'E2E 測試員', password: PASSWORD })
  await member.submitRegister()
  await expect(member.page.getByText('會員狀態：Pending')).toBeVisible()
  const token = await mailpit.waitForToken(email, '請驗證您的會員信箱')
  await member.gotoVerifyEmail(token)
  await expect(member.page.getByText('會員狀態：Active')).toBeVisible()
}

export async function login(member: MemberPage, account: string) {
  await member.gotoLogin()
  await member.fillLoginForm(account, PASSWORD)
  await member.submitLogin()
  await member.page.waitForURL('**/member')
}

