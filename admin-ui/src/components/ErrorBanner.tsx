import { isAxiosError } from '../utils/isAxiosError'
import type { ApiError } from '../types'

interface ErrorBannerProps {
  error: unknown
  onDismiss?: () => void
}

export function ErrorBanner({ error, onDismiss }: ErrorBannerProps) {
  const err = isAxiosError(error) ? (error.payload as ApiError) : null
  const msg = err?.message ?? (error instanceof Error ? error.message : 'Bilinmeyen hata')

  return (
    <div className="error-banner">
      <span className="error-icon">⚠</span>
      <span className="error-msg">
        {err?.code && <code className="error-code">{err.code}: </code>}
        {msg}
      </span>
      {onDismiss && (
        <button className="error-dismiss" onClick={onDismiss}>×</button>
      )}
    </div>
  )
}
