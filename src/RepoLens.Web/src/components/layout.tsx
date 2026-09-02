import { Link } from 'react-router-dom'

export function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-svh flex-col">
      <header className="border-b">
        <div className="mx-auto flex h-14 w-full max-w-5xl items-center gap-6 px-4">
          <Link to="/" className="font-semibold tracking-tight">
            RepoLens
          </Link>
          <nav className="flex items-center gap-1 text-sm text-muted-foreground">
            <Link
              to="/discover"
              className="rounded-md px-2 py-1 hover:bg-muted hover:text-foreground"
            >
              Explore GitHub
            </Link>
            <Link
              to="/search"
              className="rounded-md px-2 py-1 hover:bg-muted hover:text-foreground"
            >
              Search RepoLens
            </Link>
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">{children}</main>
    </div>
  )
}

export function PageHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div className="mb-6">
      <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
      {subtitle ? <p className="mt-1 text-sm text-muted-foreground">{subtitle}</p> : null}
    </div>
  )
}
