// Admin key is stored in localStorage under this key
export const ADMIN_KEY_STORAGE = 'fieldops_admin_key'

export function getAdminKey(): string | null {
  return localStorage.getItem(ADMIN_KEY_STORAGE)
}

export function setAdminKey(key: string): void {
  localStorage.setItem(ADMIN_KEY_STORAGE, key)
}

export function clearAdminKey(): void {
  localStorage.removeItem(ADMIN_KEY_STORAGE)
}
