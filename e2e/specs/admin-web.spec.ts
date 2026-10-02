import type { Page } from '@playwright/test'
import { test, expect } from '../support/test-fixtures'
import type { MailpitClient } from '../support/mailpit-client'
import { MemberPage } from '../pages/member-page'
import { DeveloperPage } from '../pages/developer-page'
import { AdminPage } from '../pages/admin-page'
import { newEmail, registerAndActivate, login } from '../support/member-flow'
import { promoteToAdmin, setApplicationStatus } from '../support/db-helper'

/** 註冊啟用會員（可選升級為管理員）並登入 */
async function signIn(page: Page, mailpit: MailpitClient, admin: boolean) {
  const email = newEmail()
  const member = new MemberPage(page)
  await registerAndActivate(member, mailpit, email)
  if (admin) promoteToAdmin(email) // 須在登入前：role 於換票時寫入 Token
  await login(member, email)
  const { id } = (await member.api('GET', '/api/v1/member/profile')).body
  return { email, memberId: id as string }
}

/** 以開發者後台建立應用（預設 Active），回傳 id */
async function createApp(page: Page, email: string) {
  const dev = new DeveloperPage(page)
  await dev.gotoNewApp()
  await dev.fillAppForm({ name: `E2E 審核 ${Date.now()}`, description: 'E2E 測試用應用', contactEmail: email })
  await dev.submitAppForm()
  await page.waitForURL(/\/apps\/[0-9a-f-]{36}$/)
  return page.url().split('/').pop()!
}

test('非管理員存取管理後台被拒絕 (403)', async ({ page, mailpit }) => {
  await signIn(page, mailpit, false)
  await new AdminPage(page).gotoApplications()
  await expect(page.getByRole('alert')).toContainText('此帳號不是平台管理員')
})

test('管理員 SSO 登入、導覽列身分與列表載入', async ({ page, mailpit }) => {
  const { email } = await signIn(page, mailpit, true)
  const admin = new AdminPage(page)
  await admin.gotoApplications()
  await expect(page.getByRole('heading', { name: '全平台應用程式' })).toBeVisible()
  await expect(page.locator('header nav span').last()).toHaveText(email)
  expect(await admin.getNavIdentity()).toBe(email)
  await expect(page.getByRole('alert')).toHaveCount(0)
})

test('待審核應用核准上線', async ({ page, mailpit }) => {
  const { email } = await signIn(page, mailpit, true)
  const id = await createApp(page, email)
  setApplicationStatus(id, 1)

  const admin = new AdminPage(page)
  await admin.gotoApplications()
  await admin.filterByStatus('PendingReview')
  await page.locator(`a[href="/applications/${id}"]`).click()
  await expect(page.getByText('待審核', { exact: true })).toBeVisible()

  await admin.clickApprove()
  await expect(page.getByText('啟用中', { exact: true })).toBeVisible()
})

test('緊急斷路停用與取消停用', async ({ page, mailpit }) => {
  const { email } = await signIn(page, mailpit, true)
  const id = await createApp(page, email)

  const admin = new AdminPage(page)
  await admin.gotoAppDetail(id)
  await admin.suspendApp('E2E 測試：涉嫌違規緊急斷路')
  await expect(page.getByText('已停用', { exact: true })).toBeVisible()
  await expect(page.getByRole('status')).toContainText(/已作廢 \d+ 把 API Key、\s*\d+ 筆授權/)

  await admin.clickUnsuspend()
  await expect(page.getByText('啟用中', { exact: true })).toBeVisible()
})

test('稽核日誌可查到停用與啟用紀錄', async ({ page, mailpit }) => {
  const { email, memberId } = await signIn(page, mailpit, true)
  const id = await createApp(page, email)

  const admin = new AdminPage(page)
  await admin.gotoAppDetail(id)
  await admin.suspendApp('E2E 測試：稽核日誌')
  await expect(page.getByText('已停用', { exact: true })).toBeVisible()
  await admin.clickUnsuspend()
  await expect(page.getByText('啟用中', { exact: true })).toBeVisible()

  await admin.gotoAuditLogs({ targetId: id })
  await expect(page.locator('main ul > li')).toHaveCount(2)
  const logs = (await admin.getLogItems()).join('\n')
  expect(logs).toContain('application.suspend')
  expect(logs).toContain('application.activate')
  expect(logs).toContain(memberId) // 操作人
  expect(logs).toMatch(/\d{1,2}:\d{2}/) // 時間戳記
})
