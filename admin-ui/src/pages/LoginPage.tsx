import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../utils/api'

export default function LoginPage() {
  const navigate = useNavigate()
  const [key, setKey] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!key.trim()) return
    setLoading(true)
    setError(null)

    // Probe the API with the provided key
    localStorage.setItem('fieldops_admin_key', key.trim())
    try {
      await api.listTenants(1, 1)
      navigate('/')
    } catch (err) {
      localStorage.removeItem('fieldops_admin_key')
      setError(
        err instanceof Error
          ? err.message
          : 'Geçersiz API anahtarı veya sunucu erişilemez.',
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-title">FieldOps Admin</div>
        <p className="login-sub">
          Yönetim paneline erişmek için admin API anahtarınızı girin.
        </p>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {error && <div className="error-banner" style={{ marginBottom: 0 }}>{error}</div>}

          <div className="form-group">
            <label className="form-label" htmlFor="api-key">Admin API Key</label>
            <input
              id="api-key"
              className="form-input"
              type="password"
              placeholder="fo_live_..."
              value={key}
              onChange={(e) => setKey(e.target.value)}
              autoComplete="off"
              autoFocus
            />
          </div>

          <button type="submit" className="btn btn-primary w-full" disabled={loading || !key.trim()}>
            {loading ? <span className="spinner" /> : null}
            {loading ? 'Doğrulanıyor…' : 'Giriş Yap'}
          </button>
        </form>

        <p className="text-muted text-sm" style={{ marginTop: 20, textAlign: 'center' }}>
          Admin API key, Super Admin SPA için üretilen özel bir anahtardır.
          Tenant API key'leri ile karıştırmayın.
        </p>
      </div>
    </div>
  )
}
