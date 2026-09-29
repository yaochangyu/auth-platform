export type ApplicationStatus = 'Active' | 'PendingReview' | 'Suspended'

export interface AdminApplication {
  id: string
  ownerMemberId: string
  clientId: string
  name: string
  description: string
  contactEmail: string
  status: ApplicationStatus
  createdAt: string
  updatedAt: string
}

export interface AdminApplicationList {
  items: AdminApplication[]
  total: number
  page: number
  pageSize: number
}

export interface CircuitBreakerResult {
  revokedApiKeys: number
  revokedAuthorizations: number
  revokedTokens: number
}

export interface StatusChangeResponse {
  application: AdminApplication
  circuitBreaker: CircuitBreakerResult | null
}

export interface AuditLog {
  id: number
  occurredAt: string
  actorMemberId: string
  clientIp: string
  action: string
  targetType: string
  targetId: string
  before: Record<string, unknown> | null
  after: Record<string, unknown> | null
  details: Record<string, unknown> | null
}

export interface AuditLogList {
  items: AuditLog[]
  total: number
  page: number
  pageSize: number
}
