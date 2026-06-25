import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../utils/api'
import type {
  TenantResponse,
  TenantListResponse,
  TenantCreateRequest,
  TenantUpdateRequest,
  ApiKeyResponse,
  ApiKeyCreatedResponse,
  ApiKeyCreateRequest,
  SyncRunListResponse,
  OutboxListResponse,
  EventListResponse,
} from '../types'

// ─── Tenants ─────────────────────────────────────────────────────────────────

export function useTenants(page = 1, pageSize = 20) {
  return useQuery<TenantListResponse, Error>({
    queryKey: ['tenants', page, pageSize],
    queryFn: () => api.listTenants(page, pageSize) as Promise<TenantListResponse>,
  })
}

export function useTenant(id: string) {
  return useQuery<TenantResponse, Error>({
    queryKey: ['tenant', id],
    queryFn: () => api.getTenant(id) as Promise<TenantResponse>,
    enabled: !!id,
  })
}

export function useCreateTenant() {
  const qc = useQueryClient()
  return useMutation<TenantResponse, Error, TenantCreateRequest>({
    mutationFn: (body) => api.createTenant(body) as Promise<TenantResponse>,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tenants'] }),
  })
}

export function useUpdateTenant() {
  const qc = useQueryClient()
  return useMutation<
    TenantResponse,
    Error,
    { id: string; body: TenantUpdateRequest }
  >({
    mutationFn: ({ id, body }) =>
      api.updateTenant(id, body) as Promise<TenantResponse>,
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: ['tenants'] })
      qc.invalidateQueries({ queryKey: ['tenant', id] })
    },
  })
}

export function useDeleteTenant() {
  const qc = useQueryClient()
  return useMutation<void, Error, string>({
    mutationFn: (id) => api.deleteTenant(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tenants'] }),
  })
}

// ─── API Keys ────────────────────────────────────────────────────────────────

export function useApiKeys(tenantId: string) {
  return useQuery<ApiKeyResponse[], Error>({
    queryKey: ['keys', tenantId],
    queryFn: () => api.listKeys(tenantId) as Promise<ApiKeyResponse[]>,
    enabled: !!tenantId,
  })
}

export function useCreateApiKey() {
  const qc = useQueryClient()
  return useMutation<
    ApiKeyCreatedResponse,
    Error,
    { tenantId: string; body: ApiKeyCreateRequest }
  >({
    mutationFn: ({ tenantId, body }) =>
      api.createKey(tenantId, body) as Promise<ApiKeyCreatedResponse>,
    onSuccess: (_, { tenantId }) =>
      qc.invalidateQueries({ queryKey: ['keys', tenantId] }),
  })
}

export function useRevokeApiKey() {
  const qc = useQueryClient()
  return useMutation<void, Error, { tenantId: string; keyId: string }>({
    mutationFn: ({ tenantId, keyId }) => api.revokeKey(tenantId, keyId),
    onSuccess: (_, { tenantId }) =>
      qc.invalidateQueries({ queryKey: ['keys', tenantId] }),
  })
}

// ─── Sync Runs ─────────────────────────────────────────────────────────────

export function useSyncRuns(tenantId: string, page = 1, pageSize = 20) {
  return useQuery<SyncRunListResponse, Error>({
    queryKey: ['syncRuns', tenantId, page, pageSize],
    queryFn: () =>
      api.listSyncRuns(tenantId, page, pageSize) as Promise<SyncRunListResponse>,
    enabled: !!tenantId,
  })
}

// ─── Outbox ─────────────────────────────────────────────────────────────────

export function useOutbox(tenantId: string, page = 1, pageSize = 20) {
  return useQuery<OutboxListResponse, Error>({
    queryKey: ['outbox', tenantId, page, pageSize],
    queryFn: () =>
      api.listOutbox(tenantId, page, pageSize) as Promise<OutboxListResponse>,
    enabled: !!tenantId,
  })
}

// ─── Agent Events ──────────────────────────────────────────────────────────

export function useAgentEvents(tenantId: string, page = 1, pageSize = 50) {
  return useQuery<EventListResponse, Error>({
    queryKey: ['events', tenantId, page, pageSize],
    queryFn: () =>
      api.listEvents(tenantId, page, pageSize) as Promise<EventListResponse>,
    enabled: !!tenantId,
  })
}
