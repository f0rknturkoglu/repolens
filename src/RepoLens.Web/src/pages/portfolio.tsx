import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Briefcase, Search, Sparkles } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { ApiError, getJson } from '@/lib/api'
import { z } from 'zod'

const coverageEntrySchema = z.object({
  category: z.string(),
  evidenceCount: z.number(),
  band: z.enum(['strong', 'moderate', 'limited']),
  repositories: z.array(z.string()),
})

const portfolioResponseSchema = z.object({
  id: z.number(),
  username: z.string(),
  version: z.string(),
  status: z.string(),
  analyzedRepositoryCount: z.number(),
  totalRepositoryCount: z.number(),
  createdAtUtc: z.string(),
  completedAtUtc: z.string().nullable(),
  coverage: z.object({ entries: z.array(coverageEntrySchema) }),
  signals: z.array(
    z.object({
      category: z.string(),
      repositoryFullName: z.string(),
      reason: z.string(),
      stars: z.number(),
    }),
  ),
})

const marginalResponseSchema = z.object({
  username: z.string(),
  score: z.number(),
  formulaVersion: z.string(),
  categories: z.array(
    z.object({ category: z.string(), currentBand: z.string(), value: z.number(), rationale: z.string() }),
  ),
})

type PortfolioResponse = z.infer<typeof portfolioResponseSchema>
type MarginalResponse = z.infer<typeof marginalResponseSchema>

function fetchPortfolio(username: string, signal?: AbortSignal) {
  return getJson(`/api/portfolio/${encodeURIComponent(username)}`, portfolioResponseSchema, signal)
}

function computeMarginal(username: string, idea: string, signal?: AbortSignal): Promise<MarginalResponse> {
  return getJson(`/api/portfolio/${encodeURIComponent(username)}/marginal`, marginalResponseSchema, signal, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ idea }),
  })
}

const SAMPLE_IDEA = 'A PostgreSQL migration load-testing laboratory'

export function PortfolioPage() {
  const [username, setUsername] = useState('')
  const [portfolio, setPortfolio] = useState<PortfolioResponse | null>(null)
  const [marginalIdea, setMarginalIdea] = useState(SAMPLE_IDEA)
  const [marginal, setMarginal] = useState<MarginalResponse | null>(null)

  const analyze = useMutation({
    mutationFn: (name: string) => fetchPortfolio(name),
    onSuccess: (data) => {
      setPortfolio(data)
      setMarginal(null)
    },
  })

  const margin = useMutation({
    mutationFn: (idea: string) => computeMarginal(analyze.variables ?? username, idea),
    onSuccess: (data) => setMarginal(data),
  })

  const submitPortfolio = (e: React.FormEvent) => {
    e.preventDefault()
    const name = username.trim()
    if (!name) return
    analyze.mutate(name)
  }

  const submitMarginal = (e: React.FormEvent) => {
    e.preventDefault()
    const idea = marginalIdea.trim()
    if (!idea || !portfolio) return
    margin.mutate(idea)
  }

  const strong = portfolio?.coverage.entries.filter((e) => e.band === 'strong') ?? []
  const moderate = portfolio?.coverage.entries.filter((e) => e.band === 'moderate') ?? []
  const limited = portfolio?.coverage.entries.filter((e) => e.band === 'limited') ?? []

  return (
    <div>
      <PageHeader
        title="Analyze Portfolio"
        subtitle="Evidence-backed coverage of a public GitHub profile. Portfolio signals only — RepoLens never claims to grade your skills."
      />

      <form onSubmit={submitPortfolio} className="mb-6 flex flex-col gap-2 sm:flex-row" role="search">
        <input
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          placeholder="GitHub username (public profile)"
          aria-label="GitHub username"
          className="h-9 flex-1 rounded-lg border bg-background px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
        />
        <Button type="submit" disabled={analyze.isPending} className="gap-1.5">
          <Search data-slot="icon" />
          {analyze.isPending ? 'Analyzing…' : 'Analyze'}
        </Button>
      </form>

      {analyze.isPending && (
        <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">
          Fetching public repositories and extracting signals…
        </div>
      )}

      {analyze.isError && (
        <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-medium text-destructive">Analysis failed</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {analyze.error instanceof ApiError ? analyze.error.message : 'Something went wrong.'}
          </p>
        </div>
      )}

      {portfolio && (
        <div>
          <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            <Badge variant="secondary">
              <Briefcase data-slot="icon" />
              {portfolio.totalRepositoryCount} public repos · {portfolio.analyzedRepositoryCount} owned
            </Badge>
            <Badge variant="outline">portfolio v{portfolio.version}</Badge>
          </div>

          <CoverageSection title="Strong signals" entries={strong} tone="text-emerald-600" />
          <CoverageSection title="Moderate signals" entries={moderate} tone="text-amber-600" />
          <CoverageSection title="Limited evidence" entries={limited} tone="text-muted-foreground" />

          <section className="mt-4 rounded-lg border p-4">
            <h2 className="text-sm font-medium">Candidate project marginal value</h2>
            <p className="mt-1 text-xs text-muted-foreground">
              How much NEW evidence a candidate project would add to this portfolio.
            </p>
            <form onSubmit={submitMarginal} className="mt-3 flex flex-col gap-2 sm:flex-row">
              <input
                value={marginalIdea}
                onChange={(e) => setMarginalIdea(e.target.value)}
                aria-label="Candidate project idea"
                className="h-9 flex-1 rounded-lg border bg-background px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
              />
              <Button type="submit" variant="outline" disabled={margin.isPending} className="gap-1.5">
                <Sparkles data-slot="icon" />
                {margin.isPending ? 'Computing…' : 'Compute marginal value'}
              </Button>
            </form>

            {marginal && (
              <div className="mt-3">
                <div className="flex items-center gap-3">
                  <span className="text-2xl font-semibold tabular-nums">
                    {Math.round(marginal.score * 100)}
                    <span className="text-sm text-muted-foreground">% new-signal value</span>
                  </span>
                  <Badge variant="outline">{marginal.formulaVersion}</Badge>
                </div>
                <ul className="mt-3 grid gap-2 md:grid-cols-2">
                  {marginal.categories.map((category) => (
                    <li key={category.category} className="rounded-md border p-3 text-sm">
                      <div className="flex items-center justify-between">
                        <span className="font-medium">{category.category}</span>
                        <Badge variant="secondary">{category.currentBand} today</Badge>
                      </div>
                      <div className="mt-1.5 h-1 overflow-hidden rounded-full bg-muted">
                        <div
                          className="h-full rounded-full bg-primary/70"
                          style={{ width: `${Math.round(category.value * 100)}%` }}
                        />
                      </div>
                      <p className="mt-1.5 text-xs text-muted-foreground">{category.rationale}</p>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </section>

          <section className="mt-4 rounded-lg border p-4">
            <h2 className="text-sm font-medium">Evidence (per repository)</h2>
            {portfolio.signals.length === 0 ? (
              <p className="mt-2 text-sm text-muted-foreground">No signals extracted yet.</p>
            ) : (
              <ul className="mt-2 flex max-h-80 flex-col gap-1 overflow-auto">
                {portfolio.signals.map((signal) => (
                  <li key={`${signal.repositoryFullName}-${signal.category}`} className="text-sm">
                    <Badge variant="ghost" className="mr-1 text-xs">
                      {signal.category}
                    </Badge>
                    <span className="font-medium">{signal.repositoryFullName}</span>
                    <span className="text-muted-foreground"> — {signal.reason}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      )}

      {!portfolio && !analyze.isPending && !analyze.isError && (
        <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">
          Enter a public GitHub username to see its portfolio coverage.
        </div>
      )}
    </div>
  )
}

function CoverageSection({
  title,
  entries,
  tone,
}: {
  title: string
  entries: { category: string; evidenceCount: number; repositories: string[] }[]
  tone: string
}) {
  if (entries.length === 0) return null
  return (
    <section className="mt-3 rounded-lg border p-4">
      <h2 className={`text-sm font-medium ${tone}`}>{title}</h2>
      <div className="mt-2 flex flex-wrap gap-1.5">
        {entries.map((entry) => (
          <Badge key={entry.category} variant="outline">
            {entry.category}
          </Badge>
        ))}
      </div>
    </section>
  )
}
