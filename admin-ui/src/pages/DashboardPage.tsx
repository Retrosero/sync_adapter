import { useNavigate } from 'react-router-dom'
import { useTenants } from '../hooks/useApi'
import { ErrorBanner } from '../components/ErrorBanner'
import { formatDate } from '../utils/format'
import type { TenantResponse } from '../types'

export default function DashboardPage() {
  const navigate = useNavigate()
  const { data, isLoading, isError, error } = useTenants(1, 100)

  const tenants = (data?.items ?? []) as TenantResponse[]

  const active = tenants.filter((t) => t.isActive).length
  const inactive = tenants.length - active
  const totalKeys = tenants.reduce((s, t) => s + t.apiKeyCount, 0)

  return (
    <div>
      <div className="page-header">
        <div>
          <div className="page-title">Dashboard</div>
          <div className="page-subtitle">FieldOps sisteminin genel durumu</div>
        </div>
      </div>

      {isError && <ErrorBanner error={error} />}

      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-label">Toplam Tenant</div>
          <div className="stat-value">{isLoading ? '…' : tenants.length}</div>
        </div>
        <div className="stat-card">
          <div className="stat-label">Aktif Tenant</div>
          <div className="stat-value success">{isLoading ? '…' : active}</div>
        </div>
        <div className="stat-card">
          <div className="stat-label">İnaktif Tenant</div>
          <div className="stat-value">{isLoading ? '…' : inactive}</div>
        </div>
        <div className="stat-card">
          <div className="stat-label">Toplam API Key</div>
          <div className="stat-value">{isLoading ? '…' : totalKeys}</div>
        </div>
      </div>

      <div className="card">
        <div className="card-header">Son Eklenen Tenantlar</div>
        <div className="table-wrap">
          {isLoading ? (
            <div className="empty-state"><span className="spinner" /></div>
          ) : tenants.length === 0 ? (
            <div className="empty-state">Henüz tenant yok.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Tenant Adı</th>
                  <th>Kod</th>
                  <th>Mikro DB</th>
                  <th>API Key</th>
                  <th>Oluşturulma</th>
                  <th>Durum</th>
                </tr>
              </thead>
              <tbody>
                {[...tenants]
                  .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
                  .slice(0, 8)
                  .map((t) => (
                    <tr
                      key={t.id}
                      style={{ cursor: 'pointer' }}
                      onClick={() => navigate(`/tenants/${t.id}`)}
                    >
                      <td style={{ fontWeight: 600 }}>{t.name}</td>
                      <td><code className="mono">{t.code}</code></td>
                      <td className="text-muted text-sm">
                        {t.mikroServer && t.mikroDatabase
                          ? `${t.mikroServer}/${t.mikroDatabase}`
                          : '—'}
                      </td>
                      <td>
                        <span className="badge badge-neutral">{t.apiKeyCount} key</span>
                      </td>
                      <td className="text-muted text-sm">{formatDate(t.createdAt)}</td>
                      <td>
                        <span className={`badge ${t.isActive ? 'badge-success' : 'badge-neutral'}`}>
                          {t.isActive ? 'Aktif' : 'İnaktif'}
                        </span>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  )
}
