import {
  format,
  formatDistanceToNow,
  parseISO,
  isValid,
} from 'date-fns'
import { tr } from 'date-fns/locale'

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  const d = parseISO(iso)
  return isValid(d) ? format(d, 'dd MMM yyyy, HH:mm', { locale: tr }) : '—'
}

export function formatRelative(iso: string | null | undefined): string {
  if (!iso) return '—'
  const d = parseISO(iso)
  return isValid(d) ? formatDistanceToNow(d, { addSuffix: true, locale: tr }) : '—'
}

export function formatDuration(ms: number | null | undefined): string {
  if (ms == null) return '—'
  if (ms < 1000) return `${ms}ms`
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`
  return `${Math.floor(ms / 60_000)}m ${Math.floor((ms % 60_000) / 1000)}s`
}
