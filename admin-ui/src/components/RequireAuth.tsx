import { useState } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { getAdminKey } from '../utils/auth'

export function RequireAuth() {
  const [key] = useState(() => getAdminKey())
  if (!key) return <Navigate to="/login" replace />
  return <Outlet />
}
