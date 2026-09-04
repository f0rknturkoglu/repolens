import { useEffect, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import {
  Layers,
  Radar,
  ExternalLink,
  Lightbulb,
  Sparkles,
  ArrowRight,
  TrendingUp,
} from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { EvidenceBadge } from '@/components/evidence-badge'
import { ApiError, getJson } from '@/lib/api'
import { z } from 'zod'

const metricsSchema = z.object({
  candidateCount: z.number(),
  clusterCount: z.number(),
  largestClusterShare: z.number(),
  archivedRatio: z.number(),
  recentlyActiveRatio: z.number(),
  ageDistribution: z.array(z.object({ label: z.string(), count: z.number() })),
  starsMedian: z.number(),
  starsConcentration: z.number(),
  forkRatio: z.number(),
  similarityDensity: z.number(),
  primaryLanguages: z.array(z.object({ value: z.string(), count: z.number(), share: z.number() })),
  topics: z.array(z.object({ value: z.string(), count: z.number(), share: z.number() })),
  evidence: z.array(z.string()),
})

const analysisResponseSchema = z.object({
  id: z.number(),
  query: z.string(),
  version: z.string(),
  status: z.string(),
  error: z.string().nullable(),
  createdAtUtc: z.string(),
  completedAtUtc: z.string().nullable(),
  metrics: metricsSchema.nullable(),
  evidence: z.array(z.string()).nullable(),
  queryVariants: z.array(z.object({ label: z.string(), query: z.string() })).nullable(),
  clusters: z.array(
    z.object({
      label: z.string(),
      memberCount: z.number(),
      members: z.array(
        z.object({
          repositoryId: z.number(),
          centrality: z.number(),
          repository: z.object({
            fullName: z.string(),
            description: z.string().nullable(),
            primaryLanguage: z.string().nullable(),
            stars: z.number(),
            isArchived: z.boolean(),
            htmlUrl: z.string(),
          }),
        }),
      ),
    }),
  ),
})

type AnalysisResponse = z.infer<typeof analysisResponseSchema>

function runEcosystemAnalysis(query: string, signal?: AbortSignal): Promise<AnalysisResponse> {
  return getJson('/api/analysis/ecosystem', analysisResponseSchema, signal, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ query }),
  })
}

export function EcosystemPage() {
  const [searchParams] = useSearchParams()
  const analysisId = searchParams.get('analysis')
  const queryParam = searchParams.get('query')

  const [queryInput, setQueryInput] = useState(queryParam ?? '')
  const [result, setResult] = useState<AnalysisResponse | null>(null)

  const mutation = useMutation({
    mutationFn: (query: string) => runEcosystemAnalysis(query),
    onSuccess: (data) => setResult(data),
  })

  useEffect(() => {
    if (analysisId) {
      getJson(`/api/analysis/ecosystem/${analysisId}`, analysisResponseSchema)
        .then(setResult)
        .catch(() => undefined)
    } else if (queryParam && !result) {
      setQueryInput(queryParam)
      mutation.mutate(queryParam)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [analysisId, queryParam])

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    const query = queryInput.trim()
    if (!query) return
    mutation.mutate(query)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Ecosystem Analysis"
        subtitle="Map technical landscapes: cluster boundaries, market activity, tech stack adoption, and concentration."
      />

      <form
        onSubmit={submit}
        className="flex flex-col gap-3 rounded-xl border bg-card p-4 shadow-sm sm:flex-row sm:items-center"
        role="search"
      >
        <div className="flex-1">
          <input
            value={queryInput}
            onChange={(e) => setQueryInput(e.target.value)}
            placeholder="e.g. postgres migration safety, kafka cdc, terminal ui rust…"
            aria-label="Ecosystem query"
            className="h-10 w-full rounded-lg border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-primary"
          />
        </div>
        <Button type="submit" disabled={mutation.isPending} className="gap-2 shrink-0">
          <Radar className="h-4 w-4" />
          {mutation.isPending ? 'Mapping Ecosystem…' : 'Analyze Ecosystem'}
        </Button>
      </form>

      {mutation.isPending && (
        <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed p-12 text-center text-sm text-muted-foreground">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          <p className="font-medium text-foreground">Collecting candidate repositories from GitHub…</p>
          <p className="max-w-md text-xs">
            Querying multiple variants, building similarity graph edges, and extracting connected component clusters.
          </p>
        </div>
      )}

      {mutation.isError && (
        <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-semibold text-destructive">Analysis failed</p>
          <p className="mt-1 text-xs text-muted-foreground">
            {mutation.error instanceof ApiError ? mutation.error.message : 'Something went wrong.'}
          </p>
        </div>
      )}

      {result?.status === 'failed' && (
        <div className="rounded-xl border p-6">
          <p className="text-sm font-medium">Analysis could not complete</p>
          <p className="mt-1 text-xs text-muted-foreground">
            {result.error === 'NoCandidates'
              ? 'No repositories matched any query variant in the search window.'
              : 'GitHub could not be reached or rejected the query variants.'}
          </p>
        </div>
      )}

      {result && result.status === 'completed' && result.metrics && (
        <div className="space-y-6">
          {/* Metadata bar */}
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/20 px-4 py-2 text-xs text-muted-foreground">
            <div className="flex items-center gap-2">
              <EvidenceBadge
                type="deterministic"
                label={`ecosystem-v${result.version}`}
                subtext="Connected graph components and Jaccard-token similarity"
              />
              <Badge variant="secondary" className="text-[11px]">
                {result.metrics.candidateCount} candidate repos analyzed
              </Badge>
            </div>
            <span>Analysis run #{result.id} &bull; {result.metrics.clusterCount} clusters discovered</span>
          </div>

          {/* Key Metrics Grid */}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <MetricCard label="Candidate Pool" value={String(result.metrics.candidateCount)} />
            <MetricCard label="Distinct Clusters" value={String(result.metrics.clusterCount)} />
            <MetricCard
              label="Dominant Cluster Share"
              value={`${Math.round(result.metrics.largestClusterShare * 100)}%`}
            />
            <MetricCard label="Median Stars" value={result.metrics.starsMedian.toLocaleString()} />
            <MetricCard
              label="Archived Ratio"
              value={`${Math.round(result.metrics.archivedRatio * 100)}%`}
            />
            <MetricCard
              label="Recently Active (90d)"
              value={`${Math.round(result.metrics.recentlyActiveRatio * 100)}%`}
            />
            <MetricCard
              label="Star Concentration (Top 5)"
              value={`${Math.round(result.metrics.starsConcentration * 100)}%`}
            />
            <MetricCard
              label="Similarity Density"
              value={Math.round(result.metrics.similarityDensity * 100) + '%'}
            />
          </div>

          {/* Primary Language and Topics Distribution */}
          <div className="grid gap-4 lg:grid-cols-2">
            <DistributionCard
              title="Primary Languages"
              rows={result.metrics.primaryLanguages.slice(0, 8).map((l) => ({
                label: l.value,
                share: l.share,
                detail: `${l.count} repo${l.count === 1 ? '' : 's'}`,
              }))}
            />
            <DistributionCard
              title="Top Technical Topics"
              rows={result.metrics.topics.slice(0, 8).map((t) => ({
                label: t.value,
                share: t.share,
                detail: `${t.count} repo${t.count === 1 ? '' : 's'}`,
              }))}
            />
          </div>

          <AgeDistributionCard
            buckets={result.metrics.ageDistribution}
            total={result.metrics.candidateCount}
          />

          {/* Clusters Section */}
          <section className="rounded-xl border bg-card p-5 shadow-sm space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-foreground">Discovered Ecosystem Clusters</h3>
                <p className="text-xs text-muted-foreground">
                  Groups of projects connected by topic overlap and structural similarity.
                </p>
              </div>
              <EvidenceBadge
                type="deterministic"
                label="Graph Components"
                subtext="Deterministic connected components from similarity matrix"
              />
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              {result.clusters.map((cluster) => (
                <div
                  key={`${cluster.label}-${cluster.memberCount}`}
                  className="flex flex-col justify-between rounded-lg border bg-background p-4 shadow-2xs space-y-3"
                >
                  <div>
                    <div className="flex items-center gap-2 border-b pb-2">
                      <Layers className="h-4 w-4 text-primary" />
                      <span className="font-semibold text-sm text-foreground">{cluster.label}</span>
                      <Badge variant="outline" className="ml-auto text-xs">
                        {cluster.memberCount} repo{cluster.memberCount === 1 ? '' : 's'}
                      </Badge>
                    </div>

                    <ul className="mt-3 space-y-2">
                      {cluster.members.slice(0, 6).map((member) => (
                        <li
                          key={member.repositoryId}
                          className="flex items-center justify-between gap-2 text-xs"
                        >
                          <div className="flex items-center gap-1.5 truncate">
                            <Link
                              to={`/repositories/${member.repositoryId}`}
                              className="font-medium text-foreground hover:text-primary truncate"
                              title="Inspect in RepoLens"
                            >
                              {member.repository.fullName}
                            </Link>
                            <a
                              href={member.repository.htmlUrl}
                              target="_blank"
                              rel="noreferrer"
                              className="text-muted-foreground hover:text-foreground shrink-0 p-0.5"
                              title="Open on GitHub"
                            >
                              <ExternalLink className="h-3 w-3" />
                            </a>
                            {member.repository.isArchived && (
                              <span className="rounded bg-destructive/10 px-1 py-0.2 text-[9px] text-destructive">
                                archived
                              </span>
                            )}
                          </div>
                          <span className="shrink-0 text-muted-foreground tabular-nums">
                            {member.repository.stars.toLocaleString()}★
                          </span>
                        </li>
                      ))}
                    </ul>

                    {cluster.memberCount > 6 && (
                      <p className="mt-2 text-[11px] text-muted-foreground">
                        +{cluster.memberCount - 6} more in this cluster
                      </p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* Evidence Observations */}
          <section className="rounded-xl border bg-card p-5 shadow-sm space-y-3">
            <h3 className="text-sm font-bold text-foreground">Observed Evidence Summary</h3>
            <ul className="space-y-1.5">
              {result.evidence?.map((line) => (
                <li key={line} className="flex items-start gap-2 text-xs text-muted-foreground">
                  <span className="text-primary font-bold">&bull;</span>
                  <span>{line}</span>
                </li>
              ))}
            </ul>
          </section>

          {/* Action Bridges (Primary Decision Loop) */}
          <section className="rounded-xl border border-primary/20 bg-primary/5 p-6 space-y-4">
            <div className="flex items-center gap-2">
              <Sparkles className="h-5 w-5 text-primary" />
              <h3 className="font-bold text-base text-foreground">Next Steps: Build in This Space</h3>
            </div>
            <p className="text-xs text-muted-foreground leading-relaxed">
              Use this ecosystem mapping to validate your project hypothesis or generate projects that fill market voids.
            </p>

            <div className="flex flex-wrap gap-3 pt-2">
              <Link
                to={`/validate?idea=${encodeURIComponent(`A tool for ${result.query}`)}`}
                className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-xs hover:bg-primary/90 transition-colors"
              >
                <Lightbulb className="h-4 w-4" />
                <span>Validate an Idea in This Ecosystem</span>
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>

              <Link
                to={`/recommend?goal=${encodeURIComponent(result.query)}`}
                className="inline-flex items-center gap-2 rounded-lg border bg-background px-4 py-2 text-xs font-medium text-foreground hover:bg-muted/50 transition-colors"
              >
                <TrendingUp className="h-4 w-4" />
                <span>Find Project Opportunities in This Domain</span>
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </div>
          </section>

          {/* Candidate Bounded Disclosure */}
          <p className="text-xs text-muted-foreground text-center">
            Analysis computed from GitHub search results collected at query time. Trends reflect the analyzed candidate snapshot ({result.metrics.candidateCount} repositories).
          </p>
        </div>
      )}

      {!result && !mutation.isPending && !mutation.isError && (
        <div className="rounded-xl border border-dashed p-10 text-center text-xs text-muted-foreground space-y-2">
          <Layers className="mx-auto h-8 w-8 opacity-40" />
          <p className="font-medium text-foreground text-sm">Enter a technical domain to map its ecosystem</p>
          <p className="max-w-md mx-auto">
            RepoLens will gather candidate repositories, analyze language distributions, compute connected graph clusters, and assess market saturation.
          </p>
        </div>
      )}
    </div>
  )
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border bg-card p-3.5 shadow-2xs">
      <p className="text-xs text-muted-foreground font-medium">{label}</p>
      <p className="mt-1 text-2xl font-bold tracking-tight tabular-nums text-foreground">{value}</p>
    </div>
  )
}

function AgeDistributionCard({
  buckets,
  total,
}: {
  buckets: { label: string; count: number }[]
  total: number
}) {
  return (
    <DistributionCard
      title="Age Distribution"
      rows={buckets.map((bucket) => ({
        label: bucket.label,
        share: bucket.count / Math.max(total, 1),
        detail: `${bucket.count} repo${bucket.count === 1 ? '' : 's'}`,
      }))}
      className="mt-4"
    />
  )
}

function DistributionCard({
  title,
  rows,
  className = '',
}: {
  title: string
  rows: { label: string; share: number; detail: string }[]
  className?: string
}) {
  if (rows.length === 0) return null
  return (
    <section className={`rounded-xl border bg-card p-5 shadow-sm ${className}`}>
      <h3 className="text-sm font-bold text-foreground">{title}</h3>
      <ul className="mt-3 space-y-2.5">
        {rows.map((row) => (
          <li key={row.label} className="space-y-1">
            <div className="flex justify-between text-xs">
              <span className="font-medium text-foreground">{row.label}</span>
              <span className="text-muted-foreground">{row.detail}</span>
            </div>
            <div className="h-1.5 overflow-hidden rounded-full bg-muted">
              <div
                className="h-full rounded-full bg-primary/70 transition-all"
                style={{ width: `${Math.min(Math.round(row.share * 100), 100)}%` }}
              />
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
