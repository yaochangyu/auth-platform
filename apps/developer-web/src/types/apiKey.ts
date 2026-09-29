export type ApiKeyEnvironment = 'Live' | 'Test'

export interface ApiKeySummary {
  id: string
  name: string
  environment: ApiKeyEnvironment
  prefix: string
  scopes: string[]
  createdAt: string
  expiresAt: string | null
  revokedAt: string | null
  status: 'Active' | 'Expired' | 'Revoked'
}

export interface ApiKeyListResponse {
  items: ApiKeySummary[]
  allowedScopes: string[]
}

export interface ApiKeyRequest {
  name: string
  environment: ApiKeyEnvironment
  scopes: string[]
  expiresAt: string | null
}

// API Key 與 API Secret 明文只會在發行當下回傳一次。
export interface IssuedApiKey {
  id: string
  apiKey: string
  apiSecret: string
  prefix: string
  scopes: string[]
  createdAt: string
  expiresAt: string | null
}
