import { getAdminKey } from './auth'
import type { ApiError } from '../types'

// Production'da aynı domain altında sunulursa /api/v1 yeterli.
// Farklı subdomain kullanılacaksa VITE_API_URL ortam değişkeni ile override edilir.
// Örnek: VITE_API_URL=https://api.alanadin.com → API: https://api.alanadin.com/api/v1
// Örnek: VITE_API_URL yok → /api/v1 (aynı origin, Nginx proxy gerektirir)
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api/v1'
const BASE = `${API_BASE}/v1`.replace('/v1/v1', '/v1') // double-slash koruma

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
): Promise<T> {
  const key = getAdminKey()
  if (!key) throw new Error('Not authenticated')

  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-Api-Key': key,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (!res.ok) {
    const err: ApiError = await res.json().catch(() => ({
      code: 'UNKNOWN',
      message: res.statusText,
    }))
    throw err
  }

  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export const api = {
  // Tenants
  listTenants: (page = 1, pageSize = 20) =>
    request<{ items: unknown[]; totalCount: number; page: number; pageSize: number }>(
      'GET',
      `/admin/tenants?page=${page}&pageSize=${pageSize}`,
    ),

  getTenant: (id: string) =>
    request<unknown>('GET', `/admin/tenants/${id}`),

  createTenant: (body: unknown) =>
    request<unknown>('POST', '/admin/tenants', body),

  updateTenant: (id: string, body: unknown) =>
    request<unknown>('PATCH', `/admin/tenants/${id}`, body),

  deleteTenant: (id: string) =>
    request<void>('DELETE', `/admin/tenants/${id}`),

  // API Keys
  listKeys: (tenantId: string) =>
    request<unknown[]>('GET', `/admin/tenants/${tenantId}/keys`),

  createKey: (tenantId: string, body: unknown) =>
    request<unknown>('POST', `/admin/tenants/${tenantId}/keys`, body),

  revokeKey: (tenantId: string, keyId: string) =>
    request<void>('POST', `/admin/tenants/${tenantId}/keys/${keyId}/revoke`),

  // Sync Runs
  listSyncRuns: (tenantId: string, page = 1, pageSize = 20) =>
    request<{ items: unknown[]; totalCount: number }>(
      'GET',
      `/admin/tenants/${tenantId}/sync-runs?page=${page}&pageSize=${pageSize}`,
    ),

  // Outbox
  listOutbox: (tenantId: string, page = 1, pageSize = 20) =>
    request<{ items: unknown[]; totalCount: number }>(
      'GET',
      `/admin/tenants/${tenantId}/outbox?page=${page}&pageSize=${pageSize}`,
    ),

  // Agent Events
  listEvents: (tenantId: string, page = 1, pageSize = 50) =>
    request<{ items: unknown[]; totalCount: number }>(
      'GET',
      `/admin/tenants/${tenantId}/events?page=${page}&pageSize=${pageSize}`,
    ),

  // Dashboard stats
  getDashboard: () =>
    request<unknown>('GET', '/admin/dashboard'),
}
