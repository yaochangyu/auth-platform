export type MemberStatus = 'Pending' | 'Active' | 'Suspended'

export interface RegisterRequest {
  email: string
  password: string
  confirmPassword: string
  displayName: string
}

export interface RegisterResponse {
  memberId: string
  email: string
  status: MemberStatus
  message: string
}

export interface VerifyEmailResponse {
  memberId: string
  email: string
  status: MemberStatus
  message: string
}

export interface LoginRequest {
  email: string
  password: string
  returnUrl?: string | null
}

export interface LoginResponse {
  memberId: string
  email: string
  displayName: string
  status: MemberStatus
  returnUrl?: string | null
}

export interface LogoutResponse {
  message: string
}

export interface ProblemDetails {
  type: string
  title: string
  status: number
  detail?: string
  errors?: Record<string, string[]>
  failedLoginAttempts?: number
  lockoutEndAt?: string
}
