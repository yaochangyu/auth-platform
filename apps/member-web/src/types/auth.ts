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

export interface ForgotPasswordRequest {
  email: string
}

export interface ForgotPasswordResponse {
  message: string
}

export interface ResetPasswordRequest {
  verificationToken: string
  newPassword: string
  confirmPassword: string
}

export interface ResetPasswordResponse {
  message: string
}

export interface MemberProfileResponse {
  id: string
  email: string
  displayName: string
  status: MemberStatus
  emailVerifiedAt?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

export interface ChangePasswordResponse {
  message: string
}

export interface ConnectedAppDto {
  appId: string
  appName: string
  appIdentifier: string
  logoUrl?: string | null
  scopes: string[]
  authorizedAt: string
  lastUsedAt?: string | null
}

export interface ConnectedAppListResponse {
  items: ConnectedAppDto[]
  totalCount: number
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
