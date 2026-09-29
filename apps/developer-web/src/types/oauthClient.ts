export type OAuthClientType = 'Public' | 'Confidential'

export interface ClientSecretSummary {
  id: string
  prefix: string
  createdAt: string
  expiresAt: string | null
  revokedAt: string | null
  status: 'Active' | 'Expiring' | 'Expired' | 'Revoked'
}

export interface OAuthClientRequest {
  clientType: OAuthClientType
  redirectUris: string[]
  postLogoutRedirectUris: string[]
  scopes: string[]
}

export interface OAuthClientDto extends OAuthClientRequest {
  clientId: string
  allowedScopes: string[]
  secrets: ClientSecretSummary[]
}

// 明文只會在發行當下回傳一次。
export interface IssuedClientSecret {
  id: string
  secret: string
  prefix: string
  createdAt: string
  expiresAt: string | null
}
