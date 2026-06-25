import { Link, useLocation } from 'react-router-dom'
import { cn } from '../utils/cn'

const nav = [
  { label: 'Dashboard', to: '/' },
  { label: 'Tenantlar', to: '/tenants' },
]

export function Layout({ children }: { children?: React.ReactNode }) {
  const { pathname } = useLocation()

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="topbar-brand">FieldOps Admin</div>
        <nav className="topbar-nav">
          {nav.map((n) => (
            <Link
              key={n.to}
              to={n.to}
              className={cn('nav-link', pathname === n.to && 'active')}
            >
              {n.label}
            </Link>
          ))}
        </nav>
      </header>
      <main className="main-content">{children}</main>
    </div>
  )
}
