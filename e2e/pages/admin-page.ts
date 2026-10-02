import type { Page } from '@playwright/test'

const BASE = 'http://admin.1111.com.tw:8093'

export class AdminPage {
  constructor(readonly page: Page) {}

  gotoApplications() { return this.page.goto(`${BASE}/applications`) }
  gotoAppDetail(id: string) { return this.page.goto(`${BASE}/applications/${id}`) }
  gotoAuditLogs(p: { targetId?: string; action?: string } = {}) {
    const q = new URLSearchParams(Object.entries(p).filter(([, v]) => v) as [string, string][])
    return this.page.goto(`${BASE}/audit-logs${q.size ? `?${q}` : ''}`)
  }

  getNavIdentity() { return this.page.locator('header nav span').last().innerText() }

  filterByStatus(status: 'PendingReview' | 'Active' | 'Suspended' | '') {
    return this.page.locator('#statusFilter').selectOption(status)
  }

  clickApprove() { return this.page.getByRole('button', { name: '核准上線' }).click() }
  async suspendApp(reason: string) {
    await this.page.locator('#reason').fill(reason)
    await this.page.getByRole('button', { name: '停用應用程式（緊急斷路）' }).click()
    await this.page.getByRole('alertdialog').getByRole('button', { name: '確定停用' }).click()
  }
  clickUnsuspend() { return this.page.getByRole('button', { name: '取消停用' }).click() }

  async searchLogs(action?: string, targetId?: string) {
    if (action !== undefined) await this.page.locator('#action').selectOption(action)
    if (targetId !== undefined) await this.page.locator('#targetId').fill(targetId)
    await this.page.getByRole('button', { name: '查詢' }).click()
  }
  /** 每筆日誌的文字內容 */
  getLogItems() { return this.page.locator('main ul > li').allInnerTexts() }
}
