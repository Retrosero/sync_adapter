// ─── Core API types ─────────────────────────────────────────────────────────

export interface TenantResponse {
  id: string
  name: string
  code: string
  contactEmail: string | null
  contactPhone: string | null
  mikroServer: string | null
  mikroDatabase: string | null
  isActive: boolean
  createdAt: string
  apiKeyCount: number
}

export interface TenantCreateRequest {
  name: string
  code: string
  contactEmail?: string
  contactPhone?: string
  mikroServer?: string
  mikroDatabase?: string
  notes?: string
}

export interface TenantUpdateRequest {
  name?: string
  contactEmail?: string
  contactPhone?: string
  mikroServer?: string
  mikroDatabase?: string
  notes?: string
  isActive?: boolean
}

export interface TenantListResponse {
  items: TenantResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ApiKeyResponse {
  id: string
  tenantId: string
  keyPrefix: string
  label: string | null
  agentId: string | null
  scope: string
  isActive: boolean
  createdAt: string
  lastUsedAt: string | null
  expiresAt: string | null
  revokedAt: string | null
}

export interface ApiKeyCreateRequest {
  label?: string
  agentId?: string
  expiresAt?: string
  scope?: string
}

/** Plain key shown only once at creation */
export interface ApiKeyCreatedResponse {
  id: string
  tenantId: string
  plainKey: string
  keyPrefix: string
  label: string | null
  createdAt: string
  expiresAt: string | null
}

export interface SyncStateResponse {
  tenantId: string
  tableName: string
  status: string
  lastRunAt: string | null
  rowsTotalSynced: number
  rowsInLastRun: number
  checkpoint: string | null
  isInitial: boolean
  deadLettered: boolean
  retryCount: number
  lastError: string | null
}

export interface SyncRunResponse {
  id: string
  tenantId: string
  direction: string
  tableName: string | null
  agentId: string | null
  status: string
  startedAt: string
  finishedAt: string | null
  durationMs: number | null
  rowsTotal: number
  rowsSynced: number
  rowsFailed: number
  errorMessage: string | null
  errorCategory: string | null
}

export interface SyncRunListResponse {
  items: SyncRunResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface OutboxItemResponse {
  id: string
  tenantId: string
  idempotencyKey: string
  documentType: string
  payload: Record<string, unknown>
  status: string
  deviceId: string | null
  retryCount: number
  lockedUntil: string | null
  lastError: string | null
  createdAt: string
  processedAt: string | null
  deadLetteredAt: string | null
}

export interface OutboxListResponse {
  items: OutboxItemResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface AgentEventResponse {
  id: string
  tenantId: string
  agentId: string | null
  agentVersion: string | null
  level: string
  message: string
  exception: string | null
  category: string | null
  tableName: string | null
  runId: string | null
  occurredAt: string
}

export interface EventListResponse {
  items: AgentEventResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface DashboardStats {
  totalTenants: number
  activeTenants: number
  totalApiKeys: number
  activeApiKeys: number
  totalSyncRuns: number
  failedSyncRuns: number
  pendingOutboxItems: number
  deadLetteredItems: number
}

// ─── API Error ───────────────────────────────────────────────────────────────

export interface ApiError {
  code: string
  message: string
  traceId?: string
  fieldErrors?: Record<string, string[]>
}
