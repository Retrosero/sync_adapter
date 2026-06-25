import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { useTenant, useUpdateTenant, useApiKeys, useCreateApiKey, useRevokeApiKey, useSyncRuns, useOutbox, useAgentEvents } from '../hooks/useApi'
import { ErrorBanner } from '../components/ErrorBanner'
import { formatDate, formatRelative, formatDuration } from '../utils/format'
import type {
  ApiKeyResponse,
  ApiKeyCreatedResponse,
  TenantUpdateRequest,
  SyncRunResponse,
  OutboxItemResponse,
  AgentEventResponse,
} from '../types'

type Tab = 'info' | 'keys' | 'sync' | 'outbox' | 'events'

export default function TenantDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [tab, setTab] = useState<Tab>('info')
  const [showCreateKey, setShowCreateKey] = useState(false)
  const [createdKey, setCreatedKey] = useState<ApiKeyCreatedResponse | null>(null)

  const tenant = useTenant(id)
  const update = useUpdateTenant()
  const keys = useApiKeys(id)
  const createKey = useCreateApiKey()
  const revokeKey = useRevokeApiKey()
  const syncRuns = useSyncRuns(id, 1, 20)
  const outbox = useOutbox(id, 1, 20)
  const events = useAgentEvents(id, 1, 50)

  function handleUpdateStatus(active: boolean) {
    update.mutate({ id, body: { isActive: active } })
  }

  const apiKeys = (keys.data ?? []) as ApiKeyResponse[]

  return (
    <div>
      {/* Breadcrumb */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 20, fontSize: 13, color: 'var(--text-muted)' }}>
        <span style={{ cursor: 'pointer' }} onClick={() => navigate('/tenants')}>Tenantlar</span>
        <span>›</span>
        <span>{tenant.data?.name ?? '…'}</span>
      </div>

      <div className="page-header">
        <div>
          <div className="page-title">{tenant.data?.name ?? 'Tenant Detay'}</div>
          <div className="page-subtitle">Tenant ID: <code>{id}</code></div>
        </div>
        {tenant.data && (
          <div className="flex gap-2">
            <button
              className={`btn ${tenant.data.isActive ? 'btn-ghost' : 'btn-primary'}`}
              onClick={() => handleUpdateStatus(!tenant.data!.isActive)}
              disabled={update.isPending}
            >
              {tenant.data.isActive ? 'Pasif Et' : 'Aktif Et'}
            </button>
          </div>
        )}
      </div>

      {tenant.isError && <ErrorBanner error={tenant.error} />}
      {update.isError && <ErrorBanner error={update.error} onDismiss={() => {}} />}
      {createKey.isError && <ErrorBanner error={createKey.error} onDismiss={() => {}} />}
      {revokeKey.isError && <ErrorBanner error={revokeKey.error} onDismiss={() => {}} />}

      {/* Created key shown once */}
      {createdKey && (
        <div className="card" style={{ marginBottom: 20, borderColor: 'var(--success)', background: 'var(--success-light)' }}>
          <div className="card-body">
            <div style={{ fontWeight: 700, marginBottom: 8, color: 'var(--success)' }}>
              API Key Oluşturuldu — Bir kez gösterilecek!
            </div>
            <div className="key-display">{createdKey.plainKey}</div>
            <div className="key-warning">⚠ Bu anahtarı güvenli bir yere kaydedin. Bir daha gösterilmeyecek.</div>
            <button className="btn btn-primary btn-sm mt-4" onClick={() => setCreatedKey(null)}>
              Anladım, Kapat
            </button>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div className="tabs">
        {(['info', 'keys', 'sync', 'outbox', 'events'] as Tab[]).map((t) => (
          <button key={t} className={`tab ${tab === t ? 'active' : ''}`} onClick={() => setTab(t)}>
            {tabLabel(t)}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {tab === 'info' && <InfoTab tenant={tenant.data} isLoading={tenant.isLoading} onUpdate={(body) => update.mutate({ id, body })} isSaving={update.isPending} />}
      {tab === 'keys' && <KeysTab keys={apiKeys} keysLoading={keys.isLoading} onCreate={() => setShowCreateKey(true)} onRevoke={(keyId) => revokeKey.mutate({ tenantId: id, keyId })} isRevoking={revokeKey.isPending} />}
      {tab === 'sync' && <SyncTab runs={syncRuns} />}
      {tab === 'outbox' && <OutboxTab data={outbox} />}
      {tab === 'events' && <EventsTab data={events} />}
    </div>
  )

  // ── Create key modal ──────────────────────────────────────────────────────
  if (showCreateKey) {
    return (
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 20, fontSize: 13, color: 'var(--text-muted)' }}>
          <span style={{ cursor: 'pointer' }} onClick={() => navigate('/tenants')}>Tenantlar</span>
          <span>›</span>
          <span style={{ cursor: 'pointer' }} onClick={() => navigate(`/tenants/${id}`)}>{tenant.data?.name}</span>
          <span>›</span>
          <span>Yeni API Key</span>
        </div>

        <CreateKeyForm
          tenantId={id}
          onBack={() => setShowCreateKey(false)}
          onCreated={(k) => { setCreatedKey(k); setShowCreateKey(false) }}
          isLoading={createKey.isPending}
        />
      </div>
    )
  }

  return null
}

function tabLabel(t: Tab) {
  return { info: 'Bilgi', keys: 'API Keyler', sync: 'Sync Log', outbox: 'Outbox', events: 'Olaylar' }[t]
}

// ── Info Tab ────────────────────────────────────────────────────────────────

function InfoTab({
  tenant,
  isLoading,
  onUpdate,
  isSaving,
}: {
  tenant: ReturnType<typeof useTenant>['data']
  isLoading: boolean
  onUpdate: (body: TenantUpdateRequest) => void
  isSaving: boolean
}) {
  const { register, handleSubmit } = useForm<TenantUpdateRequest>({
    defaultValues: {
      name: tenant?.name,
      contactEmail: tenant?.contactEmail ?? undefined,
      contactPhone: tenant?.contactPhone ?? undefined,
      mikroServer: tenant?.mikroServer ?? undefined,
      mikroDatabase: tenant?.mikroDatabase ?? undefined,
    },
  })

  if (isLoading) return <div className="empty-state"><span className="spinner" /></div>
  if (!tenant) return null

  return (
    <div className="detail-grid">
      <div className="card">
        <div className="card-header">Tenant Bilgileri</div>
        <form onSubmit={handleSubmit(onUpdate)}>
          <div className="card-body" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Tenant Adı</label>
                <input className="form-input" {...register('name')} />
              </div>
              <div className="form-group">
                <label className="form-label">Kod</label>
                <input className="form-input" value={tenant.code} readOnly />
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">E-posta</label>
                <input className="form-input" type="email" {...register('contactEmail')} />
              </div>
              <div className="form-group">
                <label className="form-label">Telefon</label>
                <input className="form-input" {...register('contactPhone')} />
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Mikro Server</label>
                <input className="form-input" {...register('mikroServer')} />
              </div>
              <div className="form-group">
                <label className="form-label">Mikro Database</label>
                <input className="form-input" {...register('mikroDatabase')} />
              </div>
            </div>
            <div className="form-group">
              <label className="form-label">Notlar</label>
              <textarea className="form-textarea" {...register('notes')} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
              <button type="submit" className="btn btn-primary" disabled={isSaving}>
                {isSaving ? <span className="spinner" /> : null}
                Kaydet
              </button>
            </div>
          </div>
        </form>
      </div>

      <div className="detail-info-card card">
        <div className="card-header">Sistem Bilgisi</div>
        <div className="card-body">
          <div className="info-row">
            <span className="info-label">Tenant ID</span>
            <code className="info-value mono" style={{ fontSize: 10 }}>{tenant.id}</code>
          </div>
          <div className="info-row">
            <span className="info-label">Oluşturulma</span>
            <span className="info-value">{formatDate(tenant.createdAt)}</span>
          </div>
          <div className="info-row">
            <span className="info-label">API Key Sayısı</span>
            <span className="info-value">{tenant.apiKeyCount}</span>
          </div>
          <div className="info-row">
            <span className="info-label">Durum</span>
            <span className={`badge ${tenant.isActive ? 'badge-success' : 'badge-neutral'}`}>
              {tenant.isActive ? 'Aktif' : 'İnaktif'}
            </span>
          </div>
        </div>
      </div>
    </div>
  )
}

// ── Keys Tab ───────────────────────────────────────────────────────────────

function KeysTab({
  keys,
  keysLoading,
  onCreate,
  onRevoke,
  isRevoking,
}: {
  keys: ApiKeyResponse[]
  keysLoading: boolean
  onCreate: () => void
  onRevoke: (keyId: string) => void
  isRevoking: boolean
}) {
  return (
    <div>
      <div className="page-header" style={{ marginBottom: 16 }}>
        <div className="page-title text-sm" style={{ fontWeight: 500 }}>API Key Yönetimi</div>
        <button className="btn btn-primary" onClick={onCreate}>+ Yeni Key Oluştur</button>
      </div>

      <div className="card">
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Key Prefix</th>
                <th>Label</th>
                <th>Agent ID</th>
                <th>Kapsam</th>
                <th>Son Kullanım</th>
                <th>Sonlanma</th>
                <th>Durum</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {keysLoading ? (
                <tr><td colSpan={8} className="empty-state"><span className="spinner" /></td></tr>
              ) : keys.length === 0 ? (
                <tr><td colSpan={8} className="empty-state">Bu tenant için henüz API key yok.</td></tr>
              ) : (
                keys.map((k) => (
                  <tr key={k.id}>
                    <td><code className="mono">{k.keyPrefix}…</code></td>
                    <td>{k.label ?? '—'}</td>
                    <td className="text-muted text-sm">{k.agentId ?? '—'}</td>
                    <td><span className="badge badge-primary">{k.scope}</span></td>
                    <td className="text-muted text-sm">{k.lastUsedAt ? formatRelative(k.lastUsedAt) : 'Hiç kullanılmadı'}</td>
                    <td className="text-muted text-sm">{k.expiresAt ? formatDate(k.expiresAt) : '—'}</td>
                    <td>
                      {k.revokedAt ? (
                        <span className="badge badge-danger">İptal</span>
                      ) : k.isActive ? (
                        <span className="badge badge-success">Aktif</span>
                      ) : (
                        <span className="badge badge-neutral">İnaktif</span>
                      )}
                    </td>
                    <td>
                      {!k.revokedAt && (
                        <button
                          className="btn btn-danger btn-sm"
                          disabled={isRevoking}
                          onClick={() => onRevoke(k.id)}
                        >
                          İptal Et
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}

// ── Create Key Form ─────────────────────────────────────────────────────────

function CreateKeyForm({
  tenantId,
  onBack,
  onCreated,
  isLoading,
}: {
  tenantId: string
  onBack: () => void
  onCreated: (k: ApiKeyCreatedResponse) => void
  isLoading: boolean
}) {
  const { register, handleSubmit } = useForm()
  const createKey = useCreateApiKey()

  return (
    <div className="card" style={{ maxWidth: 480 }}>
      <div className="card-header">Yeni API Key Oluştur</div>
      <form
        onSubmit={handleSubmit((body) =>
          createKey.mutate(
            { tenantId, body: body as Parameters<typeof createKey.mutate>[0]['body'] },
            { onSuccess: (data) => onCreated(data as ApiKeyCreatedResponse) },
          )
        )}
      >
        <div className="card-body" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          {createKey.isError && <ErrorBanner error={createKey.error} />}

          <div className="form-group">
            <label className="form-label">Label (opsiyonel)</label>
            <input className="form-input" {...register('label')} placeholder="windows-ajan-1" />
          </div>

          <div className="form-group">
            <label className="form-label">Agent ID (opsiyonel)</label>
            <input className="form-input" {...register('agentId')} placeholder="PC-FINGERPRINT" />
          </div>

          <div className="form-group">
            <label className="form-label">Sonlanma Tarihi (opsiyonel)</label>
            <input className="form-input" type="date" {...register('expiresAt')} />
          </div>
        </div>
        <div className="modal-footer">
          <button type="button" className="btn btn-ghost" onClick={onBack}>İptal</button>
          <button type="submit" className="btn btn-primary" disabled={isLoading}>
            {isLoading ? <span className="spinner" /> : null}
            Oluştur
          </button>
        </div>
      </form>
    </div>
  )
}

// ── Sync Runs Tab ───────────────────────────────────────────────────────────

function SyncTab({ runs }: { runs: ReturnType<typeof useSyncRuns> }) {
  const syncRuns = (runs.data?.items ?? []) as SyncRunResponse[]
  const isLoading = runs.isLoading

  return (
    <div className="card">
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Yön</th>
              <th>Tablo</th>
              <th>Agent</th>
              <th>Başlangıç</th>
              <th>Süre</th>
              <th>Satır (synced/failed)</th>
              <th>Durum</th>
              <th>Hata</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr><td colSpan={8} className="empty-state"><span className="spinner" /></td></tr>
            ) : syncRuns.length === 0 ? (
              <tr><td colSpan={8} className="empty-state">Henüz sync kaydı yok.</td></tr>
            ) : (
              syncRuns.map((r) => (
                <tr key={r.id}>
                  <td>
                    <span className={`badge ${r.direction === 'Push' ? 'badge-primary' : 'badge-warning'}`}>
                      {r.direction}
                    </span>
                  </td>
                  <td className="text-sm">{r.tableName ?? '—'}</td>
                  <td className="mono text-sm">{r.agentId ?? '—'}</td>
                  <td className="text-muted text-sm">{formatDate(r.startedAt)}</td>
                  <td className="text-sm">{formatDuration(r.durationMs)}</td>
                  <td className="text-sm">
                    {r.rowsSynced}
                    {r.rowsFailed > 0 && <span className="text-danger"> / {r.rowsFailed} hata</span>}
                  </td>
                  <td>
                    <span className={`badge ${r.status === 'Completed' ? 'badge-success' : r.status === 'Failed' ? 'badge-danger' : 'badge-neutral'}`}>
                      {r.status}
                    </span>
                  </td>
                  <td className="text-danger text-sm truncate" title={r.errorMessage ?? undefined}>
                    {r.errorMessage ?? '—'}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ── Outbox Tab ─────────────────────────────────────────────────────────────

function OutboxTab({ data }: { data: ReturnType<typeof useOutbox> }) {
  const items = (data.data?.items ?? []) as OutboxItemResponse[]
  const isLoading = data.isLoading

  return (
    <div className="card">
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>idempotency Key</th>
              <th>Belge Türü</th>
              <th>Cihaz</th>
              <th>Durum</th>
              <th>Retry</th>
              <th>Oluşturulma</th>
              <th>Son Hata</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr><td colSpan={7} className="empty-state"><span className="spinner" /></td></tr>
            ) : items.length === 0 ? (
              <tr><td colSpan={7} className="empty-state">Outbox boş.</td></tr>
            ) : (
              items.map((item) => (
                <tr key={item.id}>
                  <td><code className="mono text-sm">{item.idempotencyKey.slice(0, 8)}…</code></td>
                  <td><span className="badge badge-neutral">{item.documentType}</span></td>
                  <td className="text-muted text-sm">{item.deviceId ?? '—'}</td>
                  <td>
                    <span className={`badge ${
                      item.status === 'Pending' ? 'badge-warning' :
                      item.status === 'DeadLettered' ? 'badge-danger' :
                      'badge-success'
                    }`}>
                      {item.status}
                    </span>
                  </td>
                  <td className="text-sm">{item.retryCount}</td>
                  <td className="text-muted text-sm">{formatRelative(item.createdAt)}</td>
                  <td className="text-danger text-sm truncate" title={item.lastError ?? undefined}>
                    {item.lastError ?? '—'}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ── Events Tab ─────────────────────────────────────────────────────────────

function EventsTab({ data }: { data: ReturnType<typeof useAgentEvents> }) {
  const items = (data.data?.items ?? []) as AgentEventResponse[]
  const isLoading = data.isLoading

  return (
    <div className="card">
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Seviye</th>
              <th>Mesaj</th>
              <th>Agent</th>
              <th>Tablo</th>
              <th>Kategori</th>
              <th>Zaman</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr><td colSpan={6} className="empty-state"><span className="spinner" /></td></tr>
            ) : items.length === 0 ? (
              <tr><td colSpan={6} className="empty-state">Henüz olay yok.</td></tr>
            ) : (
              items.map((ev) => (
                <tr key={ev.id}>
                  <td>
                    <span className={`badge ${
                      ev.level === 'Error' || ev.level === 'Fatal' ? 'badge-danger' :
                      ev.level === 'Warning' ? 'badge-warning' :
                      'badge-neutral'
                    }`}>
                      {ev.level}
                    </span>
                  </td>
                  <td className="text-sm" style={{ maxWidth: 400 }}>{ev.message}</td>
                  <td className="mono text-sm">{ev.agentId ?? '—'}</td>
                  <td className="text-sm">{ev.tableName ?? '—'}</td>
                  <td className="text-sm text-muted">{ev.category ?? '—'}</td>
                  <td className="text-muted text-sm">{formatRelative(ev.occurredAt)}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
