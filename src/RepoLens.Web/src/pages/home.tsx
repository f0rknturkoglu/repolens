import { Link } from 'react-router-dom'
import { ApiStatusBadge } from '@/components/api-status'

const flows = [
  {
    title: 'Explore GitHub',
    description: 'Search GitHub repositories and store them for analysis.',
    to: '/discover',
  },
] as const

export function HomePage() {
  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-6 px-4 py-12">
      <div className="text-center">
        <h1 className="text-4xl font-semibold tracking-tight">RepoLens</h1>
        <p className="mt-2 text-muted-foreground">GitHub Ecosystem Intelligence</p>
      </div>

      <div className="flex items-center gap-2 text-sm">
        <span className="text-muted-foreground">API Status:</span>
        <ApiStatusBadge />
      </div>

      <div className="mt-4 grid w-full max-w-2xl gap-3 sm:grid-cols-2">
        {flows.map((flow) => (
          <Link
            key={flow.to}
            to={flow.to}
            className="group rounded-lg border bg-background p-5 transition-colors hover:bg-muted/30"
          >
            <h2 className="font-medium group-hover:underline">{flow.title}</h2>
            <p className="mt-1 text-sm text-muted-foreground">{flow.description}</p>
          </Link>
        ))}
      </div>
    </div>
  )
}
