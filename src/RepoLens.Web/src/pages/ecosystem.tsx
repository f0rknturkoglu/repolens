import { useEffect, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Layers, Radar } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { ApiError } from '@/lib/api'
import { z } from 'zod'
import { getJson } from '@/lib/api'

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
  const params = new URLSearchParams(window.location.search)
  const analysisId = params.get("analysis")
  const [queryInput, setQueryInput] = useState('')
  const [result, setResult] = useState<AnalysisResponse | null>(null)

  const mutation = useMutation({
    mutationFn: (query: string) => runEcosystemAnalysis(query),
    onSuccess: (data) => setResult(data),
  })

  useEffect(() => {
    if (analysisId) {
      getJson(`/api/analysis/ecosystem/${analysisId}`, analysisResponseSchema).then(setResult).catch(() => undefined)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [analysisId])

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    const query = queryInput.trim()
    if (!query) return
    mutation.mutate(query)
  }

  return (
    <div>
      <PageHeader
        title="Ecosystem Analysis"
        subtitle="Ask how a project domain looks on GitHub: similar project groups, activity, age, language and star concentration — all evidence-backed."
      />

      <form onSubmit={submit} className="mb-4 flex flex-col gap-2 sm:flex-row" role="search">
        <input
          value={queryInput}
          onChange={(e) => setQueryInput(e.target.value)}
          placeholder="e.g. postgres migration safety"
          aria-label="Ecosystem query"
          className="h-9 flex-1 rounded-lg border bg-background px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
        />
        <Button type="submit" disabled={mutation.isPending} className="gap-1.5">
          <Radar data-slot="icon" />
          {mutation.isPending ? 'Analyzing…' : 'Analyze'}
        </Button>
      </form>

      {mutation.isPending && (
        <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">
          Collecting candidates from GitHub and computing clusters…
        </div>
      )}

      {mutation.isError && (
        <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-medium text-destructive">Analysis failed</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {mutation.error instanceof ApiError
              ? mutation.error.message
              : 'Something went wrong.'}
          </p>
        </div>
      )}

      {result?.status === 'failed' && (
        <div className="rounded-lg border p-6">
          <p className="text-sm font-medium">Analysis could not complete</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {result.error === 'NoCandidates'
              ? 'No repositories matched any query variant.'
              : 'GitHub could not be reached or rejected the queries.'}
          </p>
        </div>
      )}

      {result && result.status === 'completed' && result.metrics && (
        <div>
          <div className="mb-4 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            <Badge variant="secondary">analysis v{result.version}</Badge>
            <span>analysis #{result.id}</span>
            <span>
              {result.metrics.candidateCount} candidates · {result.metrics.clusterCount} cluster
              {result.metrics.clusterCount === 1 ? '' : 's'}
            </span>
          </div>

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <MetricCard label="Candidates" value={String(result.metrics.candidateCount)} />
            <MetricCard label="Clusters" value={String(result.metrics.clusterCount)} />
            <MetricCard
              label="Largest cluster"
              value={`${Math.round(result.metrics.largestClusterShare * 100)}%`}
            />
            <MetricCard label="Median stars" value={result.metrics.starsMedian.toLocaleString()} />
            <MetricCard
              label="Archived"
              value={`${Math.round(result.metrics.archivedRatio * 100)}%`}
            />
            <MetricCard
              label="Recently active"
              value={`${Math.round(result.metrics.recentlyActiveRatio * 100)}%`}
            />
            <MetricCard
              label="Star concentration (top 5)"
              value={`${Math.round(result.metrics.starsConcentration * 100)}%`}
            />
            <MetricCard
              label="Similarity density"
              value={Math.round(result.metrics.similarityDensity * 100) + '%'}
            />
          </div>

          <div className="mt-4 grid gap-4 lg:grid-cols-2">
            <DistributionCard
              title="Primary languages"
              rows={result.metrics.primaryLanguages.slice(0, 8).map((l) => ({
                label: l.value,
                share: l.share,
                detail: `${l.count} repo${l.count === 1 ? '' : 's'}`,
              }))}
            />
            <DistributionCard
              title="Topics"
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

          <section className="mt-4 rounded-lg border p-4">
            <h2 className="text-sm font-medium">Clusters</h2>
            <ul className="mt-3 grid gap-3 md:grid-cols-2">
              {result.clusters.map((cluster) => (
                <li key={`${cluster.label}-${cluster.memberCount}`} className="rounded-md border p-3">
                  <div className="flex items-center gap-2">
                    <Layers data-slot="icon" className="size-4 text-muted-foreground" />
                    <span className="text-sm font-medium">{cluster.label}</span>
                    <Badge variant="outline" className="ml-auto">
                      {cluster.memberCount}
                    </Badge>
                  </div>
                  <ul className="mt-2 flex flex-col gap-1">
                    {cluster.members.slice(0, 6).map((member) => (
                      <li key={member.repositoryId} className="flex items-baseline gap-2 text-sm">
                        <a
                          href={member.repository.htmlUrl}
                          target="_blank"
                          rel="noreferrer"
                          className="truncate hover:underline"
                        >
                          {member.repository.fullName}
                        </a>
                        {member.repository.isArchived && (
                          <span className="text-xs text-muted-foreground">archived</span>
                        )}
                        <span className="ml-auto shrink-0 text-xs text-muted-foreground">
                          {member.repository.stars.toLocaleString()}★
                        </span>
                      </li>
                    ))}
                  </ul>
                  {cluster.memberCount > 6 && (
                    <p className="mt-1 text-xs text-muted-foreground">
                      +{cluster.memberCount - 6} more
                    </p>
                  )}
                </li>
              ))}
            </ul>
          </section>

          <section className="mt-4 rounded-lg border p-4">
            <h2 className="text-sm font-medium">Evidence</h2>
            <ul className="mt-2 flex flex-col gap-1.5">
              {result.evidence?.map((line) => (
                <li key={line} className="text-sm text-muted-foreground">
                  · {line}
                </li>
              ))}
            </ul>
          </section>

          <p className="mt-4 text-xs text-muted-foreground">
            Analysis computed from GitHub search results collected at analysis time. Trends require
            snapshot history and are intentionally not inferred from a single run.
          </p>
        </div>
      )}

      {!result && !mutation.isPending && !mutation.isError && (
        <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">
          Run an analysis to see the landscape for a project domain.
        </div>
      )}
    </div>
  )
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 text-xl font-semibold tabular-nums">{value}</p>
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
      title="Age distribution"
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
    <section className={`rounded-lg border p-4 ${className}`}>
      <h2 className="text-sm font-medium">{title}</h2>
      <ul className="mt-3 space-y-2">
        {rows.map((row) => (
          <li key={row.label}>
            <div className="flex justify-between text-xs">
              <span>{row.label}</span>
              <span className="text-muted-foreground">{row.detail}</span>
            </div>
            <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
              <div
                className="h-full rounded-full bg-primary/70"
                style={{ width: `${Math.min(Math.round(row.share * 100), 100)}%` }}
              />
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
