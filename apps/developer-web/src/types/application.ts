export type ApplicationStatus = 'Active' | 'PendingReview' | 'Suspended'

export interface ApplicationRequest {
  name: string
  description: string
  contactEmail: string
  logoUrl: string | null
  homepageUrl: string | null
}

export interface ApplicationDto extends ApplicationRequest {
  id: string
  clientId: string
  status: ApplicationStatus
  createdAt: string
  updatedAt: string
}

export interface ApplicationListResponse {
  items: ApplicationDto[]
}

export interface ProblemDetails {
  type?: string
  title: string
  status: number
  errors?: Record<string, string[]>
}
