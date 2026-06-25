import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { useTenants, useCreateTenant, useDeleteTenant } from '../hooks/useApi'
import { ErrorBanner } from '../components/ErrorBanner'
import { formatDate } from '../utils/format'
import type { TenantResponse, TenantCreateRequest } from '../types'

export default function TenantsPage() {
  const navigate = useNavigate()
  const [page, setPage] = useState(1)
  const [showCreate, setShowCreate] = useState(false)
  const [deleteId, setDeleteId] = useState<string | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  const { data, isLoading, isError, error } = useTenants(page, 20)
  const create = useCreateTenant()
  const remove = useDeleteTenant()

  const tenants = (data?.items ?? []) as TenantResponse[]
  const totalPages = data ? Math.ceil(data.totalCount / data.pageSize) : 0

  function handleDelete(id: string) {
    setDeleteError(null)
    remove.mutate(id, {
      onSuccess: () => {
        setDeleteId(null)
        if (tenants.length === 1 && page > 1) setPage((p) => p - 1)
      },
      onError: (err) => setDeleteError(err instanceof Error ? err.message : 'Silme hatası'),
    })
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <div className="page-title">Tenantlar</div>
          <div className="page-subtitle">
            {data ? `${data.totalCount} tenant` : '—'}
          </div>
        </div>
        <button className="btn btn-primary" onClick={() => setShowCreate(true)}>
          + Yeni Tenant
        </button>
      </div>

      {isError && <ErrorBanner error={error} />}
      {deleteError && <ErrorBanner error={deleteError} onDismiss={() => setDeleteError(null)} />}

      <div className="card">
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Tenant Adı</th>
                <th>Kod</th>
                <th>Mikro Server / DB</th>
                <th>E-posta</th>
                <th>Oluşturulma</th>
                <th>API Key</th>
                <th>Durum</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr><td colSpan={8} className="empty-state"><span className="spinner" /></td></tr>
              ) : tenants.length === 0 ? (
                <tr><td colSpan={8} className="empty-state">Tenant yok. "Yeni Tenant" ile ilkini ekleyin.</td></tr>
              ) : (
                tenants.map((t) => (
                  <tr key={t.id}>
                    <td>
                      <span
                        style={{ cursor: 'pointer', fontWeight: 600, color: 'var(--primary)' }}
                        onClick={() => navigate(`/tenants/${t.id}`)}
                      >
                        {t.name}
                      </span>
                    </td>
                    <td><code className="mono">{t.code}</code></td>
                    <td className="text-muted text-sm">
                      {t.mikroServer && t.mikroDatabase
                        ? `${t.mikroServer} / ${t.mikroDatabase}`
                        : '—'}
                    </td>
                    <td className="text-muted text-sm">{t.contactEmail ?? '—'}</td>
                    <td className="text-muted text-sm">{formatDate(t.createdAt)}</td>
                    <td>
                      <span className="badge badge-neutral">{t.apiKeyCount} key</span>
                    </td>
                    <td>
                      <span className={`badge ${t.isActive ? 'badge-success' : 'badge-neutral'}`}>
                        {t.isActive ? 'Aktif' : 'İnaktif'}
                      </span>
                    </td>
                    <td>
                      <div className="flex gap-2">
                        <button
                          className="btn btn-ghost btn-sm"
                          onClick={() => navigate(`/tenants/${t.id}`)}
                        >
                          Detay
                        </button>
                        <button
                          className="btn btn-danger btn-sm"
                          onClick={() => setDeleteId(t.id)}
                        >
                          Sil
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {totalPages > 1 && (
          <div className="pagination" style={{ padding: '12px 16px' }}>
            <span>{data?.totalCount ?? 0} sonuç</span>
            <div className="pagination-controls">
              <button className="btn btn-ghost btn-sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>←</button>
              <span className="text-muted">{page} / {totalPages}</span>
              <button className="btn btn-ghost btn-sm" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>→</button>
            </div>
          </div>
        )}
      </div>

      {/* Create modal */}
      {showCreate && (
        <CreateTenantModal
          onClose={() => setShowCreate(false)}
          onCreate={(body) =>
            create.mutate(body, { onSuccess: () => setShowCreate(false) })
          }
          isLoading={create.isPending}
          error={create.error ? (create.error.message ?? 'Hata') : null}
        />
      )}

      {/* Delete confirm */}
      {deleteId && (
        <div className="modal-overlay" onClick={() => setDeleteId(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()} style={{ width: 380 }}>
            <div className="modal-header">Tenant Sil</div>
            <div className="modal-body">
              <p>Bu tenant ve tüm API key'leri silinecek. Bu işlem geri alınamaz.</p>
            </div>
            <div className="modal-footer">
              <button className="btn btn-ghost" onClick={() => setDeleteId(null)}>İptal</button>
              <button className="btn btn-danger" onClick={() => handleDelete(deleteId)}>Sil</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

// ── Create tenant modal ──────────────────────────────────────────────────────

interface CreateTenantModalProps {
  onClose: () => void
  onCreate: (body: TenantCreateRequest) => void
  isLoading: boolean
  error: string | null
}

function CreateTenantModal({ onClose, onCreate, isLoading, error }: CreateTenantModalProps) {
  const { register, handleSubmit, formState: { errors } } = useForm<TenantCreateRequest>()

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          Yeni Tenant
          <button className="btn btn-ghost btn-icon" onClick={onClose}>×</button>
        </div>
        <form onSubmit={handleSubmit(onCreate)}>
          <div className="modal-body">
            {error && <ErrorBanner error={error} />}

            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Tenant Adı *</label>
                <input className="form-input" {...register('name', { required: 'Zorunlu' })} />
                {errors.name && <span className="form-error">{errors.name.message}</span>}
              </div>
              <div className="form-group">
                <label className="form-label">Kod *</label>
                <input className="form-input" {...register('code', { required: 'Zorunlu' })} placeholder="ABC" />
                {errors.code && <span className="form-error">{errors.code.message}</span>}
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
                <input className="form-input" {...register('mikroServer')} placeholder="GURBUZ" />
              </div>
              <div className="form-group">
                <label className="form-label">Mikro Database</label>
                <input className="form-input" {...register('mikroDatabase')} placeholder="MikroDB_V15_02" />
              </div>
            </div>

            <div className="form-group">
              <label className="form-label">Notlar</label>
              <textarea className="form-textarea" {...register('notes')} />
            </div>
          </div>

          <div className="modal-footer">
            <button type="button" className="btn btn-ghost" onClick={onClose}>İptal</button>
            <button type="submit" className="btn btn-primary" disabled={isLoading}>
              {isLoading ? <span className="spinner" /> : null}
              Oluştur
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
