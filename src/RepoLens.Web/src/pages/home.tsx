import { Link } from 'react-router-dom'
import { ArrowRight, Search } from 'lucide-react'
import { ApiStatusBadge } from '@/components/api-status'

const primaryActions = [
  {
    to: '/discover',
    title: 'Explore GitHub',
    description: 'Search GitHub and build your own index of what matters to you.',
  },
  {
    to: '/validate',
    title: 'Validate an Idea',
    description: 'See how saturated or original a project idea looks — with evidence.',
  },
  {
    to: '/portfolio',
    title: 'Analyze Portfolio',
    description: 'What your public GitHub profile currently demonstrates, as signals.',
  },
  {
    to: '/recommend',
    title: 'Find My Next Project',
    description: 'Three diverse, GitHub-checked projects ranked for your goal.',
  },
] as const

const secondaryActions = [
  { to: '/search', title: 'Search RepoLens', description: 'Hybrid search over repositories you already collected.' },
  { to: '/ecosystem', title: 'Ecosystem Analysis', description: 'Map a technical domain: clusters, activity, concentration.' },
] as const

export function HomePage() {
  return (
    <div className="mx-auto flex min-h-svh w-full max-w-4xl flex-col px-4 py-10">
      <div className="flex-1">
        <p className="text-sm font-medium uppercase tracking-widest text-muted-foreground">RepoLens</p>
        <h1 className="mt-2 max-w-2xl text-3xl font-semibold tracking-tight sm:text-4xl">
          GitHub Ecosystem Intelligence
        </h1>
        <p className="mt-3 max-w-2xl text-muted-foreground">
          Research what&rsquo;s already built. Validate project ideas. Understand your public
          portfolio and choose the next project with the highest evidence-backed value.
        </p>

        <div className="mt-8 grid gap-3 sm:grid-cols-2">
          {primaryActions.map((action) => (
            <Link
              key={action.to}
              to={action.to}
              className="group rounded-lg border bg-background p-4 transition-colors hover:bg-muted/40"
            >
              <span className="flex items-center justify-between font-medium">
                {action.title}
                <ArrowRight data-slot="icon" className="size-4 text-muted-foreground transition-transform group-hover:translate-x-0.5" />
              </span>
              <span className="mt-1 block text-sm text-muted-foreground">{action.description}</span>
            </Link>
          ))}
        </div>

        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          {secondaryActions.map((action) => (
            <Link
              key={action.to}
              to={action.to}
              className="group flex items-center gap-2 rounded-lg border bg-background p-4 transition-colors hover:bg-muted/40"
            >
              <Search data-slot="icon" className="size-4 text-muted-foreground" />
              <span>
                <span className="block text-sm font-medium">{action.title}</span>
                <span className="block text-sm text-muted-foreground">{action.description}</span>
              </span>
            </Link>
          ))}
        </div>

        <div className="mt-8 rounded-lg border border-dashed p-4">
          <p className="text-sm font-medium">Evidence, not vibes.</p>
          <p className="mt-1 text-sm text-muted-foreground">
            GitHub repositories → similarity → ecosystem → evidence → decision. Every number RepoLens
            shows is derived from the repositories it analyzed; scores are estimates and the evidence
            behind them is always visible. RepoLens analyzes the candidate sets it collects — it does
            not index all of GitHub.
          </p>
        </div>
      </div>

      <footer className="mt-8 flex items-center justify-end gap-2 text-xs text-muted-foreground">
        <span>API</span>
        <ApiStatusBadge />
      </footer>
    </div>
  )
}
