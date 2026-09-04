import { useEffect, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { Briefcase, Search, Sparkles, Lightbulb, ArrowRight } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { EvidenceBadge } from '@/components/evidence-badge'
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
    z.object({
      category: z.string(),
      currentBand: z.string(),
      value: z.number(),
      rationale: z.string(),
    }),
  ),
})

type PortfolioResponse = z.infer<typeof portfolioResponseSchema>
type MarginalResponse = z.infer<typeof marginalResponseSchema>

function fetchPortfolio(username: string, signal?: AbortSignal) {
  return getJson(
    `/api/portfolio/${encodeURIComponent(username)}`,
    portfolioResponseSchema,
    signal,
  )
}

function computeMarginal(
  username: string,
  idea: string,
  signal?: AbortSignal,
): Promise<MarginalResponse> {
  return getJson(
    `/api/portfolio/${encodeURIComponent(username)}/marginal`,
    marginalResponseSchema,
    signal,
    {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ idea }),
    },
  )
}

const SAMPLE_IDEA = 'A PostgreSQL migration load-testing laboratory'

export function PortfolioPage() {
  const [searchParams] = useSearchParams()
  const preseed = searchParams.get('username') ?? ''
  const [username, setUsername] = useState(preseed)
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

  useEffect(() => {
    if (preseed && !portfolio && !analyze.isPending && !analyze.isError) {
      setUsername(preseed)
      analyze.mutate(preseed)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [preseed])

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
    <div className="space-y-6">
      <PageHeader
        title="Analyze Portfolio"
        subtitle="Audit public repositories against taxonomy-v1 to identify capability signals and bridge evidence gaps."
      />

      <form
        onSubmit={submitPortfolio}
        className="flex flex-col gap-3 rounded-xl border bg-card p-4 shadow-sm sm:flex-row sm:items-center"
        role="search"
      >
        <div className="flex-1">
          <input
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder="GitHub username (e.g. torvalds, octocat)"
            aria-label="GitHub username"
            className="h-10 w-full rounded-lg border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-primary"
          />
        </div>
        <Button type="submit" disabled={analyze.isPending} className="gap-2 shrink-0">
          <Search className="h-4 w-4" />
          {analyze.isPending ? 'Auditing Public Repos…' : 'Analyze Portfolio'}
        </Button>
      </form>

      {analyze.isPending && (
        <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed p-12 text-center text-sm text-muted-foreground">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          <p className="font-medium text-foreground">Fetching public repositories from GitHub…</p>
          <p className="max-w-md text-xs">
            Extracting language, topic, and structure signals against deterministic taxonomy-v1 definitions.
          </p>
        </div>
      )}

      {analyze.isError && (
        <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-semibold text-destructive">Analysis failed</p>
          <p className="mt-1 text-xs text-muted-foreground">
            {analyze.error instanceof ApiError ? analyze.error.message : 'Something went wrong.'}
          </p>
        </div>
      )}

      {portfolio && (
        <div className="space-y-6">
          {/* Metadata bar */}
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/20 px-4 py-2 text-xs text-muted-foreground">
            <div className="flex items-center gap-2">
              <EvidenceBadge
                type="deterministic"
                label="taxonomy-v1"
                subtext="Deterministic categorization rules; never grades or ranks people"
              />
              <Badge variant="secondary" className="text-[11px]">
                <Briefcase className="mr-1 h-3 w-3" />
                {portfolio.totalRepositoryCount} public repos &bull; {portfolio.analyzedRepositoryCount} analyzed
              </Badge>
            </div>
            <span>Profile: @{portfolio.username} &bull; v{portfolio.version}</span>
          </div>

          {/* Coverage Sections */}
          <div className="space-y-4">
            <CoverageSection
              title="Strong Signals"
              entries={strong}
              tone="text-emerald-600 dark:text-emerald-400"
              badgeClass="bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/20"
              username={portfolio.username}
            />

            <CoverageSection
              title="Moderate Signals"
              entries={moderate}
              tone="text-amber-600 dark:text-amber-400"
              badgeClass="bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/20"
              username={portfolio.username}
            />

            <CoverageSection
              title="Limited Evidence (Gap Areas)"
              entries={limited}
              tone="text-muted-foreground"
              badgeClass="bg-muted text-muted-foreground border-border"
              isGapArea
              username={portfolio.username}
            />
          </div>

          {/* Marginal Value Calculator */}
          <section className="rounded-xl border bg-card p-5 shadow-sm space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-foreground">
                  Candidate Project Marginal Value
                </h3>
                <p className="text-xs text-muted-foreground">
                  Calculate how much new taxonomy evidence a proposed project would add to @{portfolio.username}&rsquo;s profile.
                </p>
              </div>
              <EvidenceBadge
                type="deterministic"
                label="marginal-v1"
                subtext="Scores band shifts from limited to moderate/strong"
              />
            </div>

            <form onSubmit={submitMarginal} className="flex flex-col gap-2 sm:flex-row">
              <input
                value={marginalIdea}
                onChange={(e) => setMarginalIdea(e.target.value)}
                placeholder="Enter a proposed project idea…"
                aria-label="Candidate project idea"
                className="h-10 flex-1 rounded-lg border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-primary"
              />
              <Button
                type="submit"
                variant="outline"
                disabled={margin.isPending}
                className="gap-2 shrink-0"
              >
                <Sparkles className="h-4 w-4" />
                {margin.isPending ? 'Calculating…' : 'Compute Marginal Value'}
              </Button>
            </form>

            {marginal && (
              <div className="mt-4 rounded-lg border bg-muted/20 p-4 space-y-4">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b pb-3">
                  <div className="flex items-baseline gap-2">
                    <span className="text-2xl font-extrabold text-foreground tabular-nums">
                      {Math.round(marginal.score * 100)}%
                    </span>
                    <span className="text-xs text-muted-foreground font-medium">
                      Net new-signal value
                    </span>
                  </div>

                  {/* Bridge to Validate this Idea */}
                  <Link
                    to={`/validate?idea=${encodeURIComponent(marginalIdea)}`}
                    className="inline-flex items-center gap-1.5 text-xs font-semibold text-primary hover:underline"
                  >
                    <Lightbulb className="h-3.5 w-3.5" />
                    <span>Validate this Idea on GitHub</span>
                    <ArrowRight className="h-3.5 w-3.5" />
                  </Link>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  {marginal.categories.map((cat) => (
                    <div
                      key={cat.category}
                      className="rounded-md border bg-background p-3 text-xs space-y-1.5"
                    >
                      <div className="flex items-center justify-between">
                        <span className="font-semibold text-foreground">{cat.category}</span>
                        <Badge variant="secondary" className="text-[10px]">
                          {cat.currentBand} today
                        </Badge>
                      </div>
                      <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
                        <div
                          className="h-full bg-primary/70"
                          style={{ width: `${Math.round(cat.value * 100)}%` }}
                        />
                      </div>
                      <p className="text-[11px] text-muted-foreground leading-relaxed">
                        {cat.rationale}
                      </p>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>

          {/* Evidence Per Repository */}
          <section className="rounded-xl border bg-card p-5 shadow-sm space-y-3">
            <h3 className="text-sm font-bold text-foreground">Extracted Signals Per Repository</h3>
            {portfolio.signals.length === 0 ? (
              <p className="text-xs text-muted-foreground py-2">
                No capability signals extracted from public repositories.
              </p>
            ) : (
              <ul className="max-h-72 divide-y overflow-y-auto pr-1">
                {portfolio.signals.map((signal) => (
                  <li
                    key={`${signal.repositoryFullName}-${signal.category}`}
                    className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 py-2.5 text-xs"
                  >
                    <div className="flex items-baseline gap-2 truncate">
                      <Badge variant="ghost" className="text-[10px] shrink-0">
                        {signal.category}
                      </Badge>
                      <span className="font-semibold text-foreground truncate">
                        {signal.repositoryFullName}
                      </span>
                      <span className="text-muted-foreground truncate">&mdash; {signal.reason}</span>
                    </div>
                    <span className="text-muted-foreground shrink-0 text-[11px] tabular-nums">
                      {signal.stars.toLocaleString()}★
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      )}

      {!portfolio && !analyze.isPending && !analyze.isError && (
        <div className="rounded-xl border border-dashed p-10 text-center text-xs text-muted-foreground space-y-2">
          <Briefcase className="mx-auto h-8 w-8 opacity-40" />
          <p className="font-medium text-foreground text-sm">
            Enter a public GitHub username to analyze its portfolio
          </p>
          <p className="max-w-md mx-auto">
            RepoLens maps public repositories to evidence signals, identifies capability gaps, and recommends high-value projects.
          </p>
        </div>
      )}
    </div>
  )
}

function CoverageSection({
  title,
  entries,
  tone,
  badgeClass,
  isGapArea = false,
  username,
}: {
  title: string
  entries: { category: string; evidenceCount: number; repositories: string[] }[]
  tone: string
  badgeClass?: string
  isGapArea?: boolean
  username: string
}) {
  if (entries.length === 0) return null

  return (
    <section className="rounded-xl border bg-card p-5 shadow-sm space-y-3">
      <div className="flex items-center justify-between">
        <h3 className={`text-sm font-bold ${tone}`}>{title}</h3>
        <span className="text-xs text-muted-foreground">
          {entries.length} categor{entries.length === 1 ? 'y' : 'ies'}
        </span>
      </div>

      <div className="flex flex-wrap gap-2">
        {entries.map((entry) => (
          <div
            key={entry.category}
            className={`inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 text-xs font-medium ${badgeClass}`}
          >
            <span>{entry.category}</span>
            {isGapArea && (
              <Link
                to={`/recommend?username=${encodeURIComponent(username)}&interests=${encodeURIComponent(entry.category)}&goal=${encodeURIComponent(`Build high-evidence project in ${entry.category}`)}`}
                className="inline-flex items-center gap-0.5 rounded bg-primary/10 px-1.5 py-0.5 text-[10px] font-semibold text-primary hover:bg-primary/20 transition-colors"
                title={`Find a project recommendation to bridge the ${entry.category} evidence gap`}
              >
                <span>Bridge Gap</span>
                <ArrowRight className="h-2.5 w-2.5" />
              </Link>
            )}
          </div>
        ))}
      </div>
    </section>
  )
}
