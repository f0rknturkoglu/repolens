import { ApiStatusBadge } from '@/components/api-status'

export function HomePage() {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-2 px-4">
      <h1 className="text-3xl font-semibold tracking-tight">RepoLens</h1>
      <p className="text-sm text-muted-foreground">GitHub Ecosystem Intelligence</p>
      <div className="mt-6 flex items-center gap-2 text-sm">
        <span className="text-muted-foreground">API Status:</span>
        <ApiStatusBadge />
      </div>
    </main>
  )
}
