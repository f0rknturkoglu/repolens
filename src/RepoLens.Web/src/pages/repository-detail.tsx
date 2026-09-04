import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useParams, useNavigate, useLocation } from 'react-router-dom'
import {
  Archive,
  ArrowLeft,
  CalendarDays,
  Code2,
  ExternalLink,
  GitFork,
  RefreshCw,
  Star,
  Network,
  Lightbulb,
  ArrowRight,
} from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { SimilarProjectsSection } from '@/components/similar-projects'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { EvidenceBadge } from '@/components/evidence-badge'
import { RefreshError, fetchRepositoryDetail, refreshRepository } from '@/lib/repositories'

export function RepositoryDetailPage() {
  const { repositoryId } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const queryClient = useQueryClient()
  const query = useQuery({
    queryKey: ['repository', repositoryId],
    queryFn: ({ signal }) => fetchRepositoryDetail(repositoryId!, signal),
    enabled: repositoryId != null && !Number.isNaN(Number(repositoryId)),
  })

  const refresh = useMutation({
    mutationFn: () => refreshRepository(repositoryId!),
    onSuccess: () => {
      // Invalidate and refetch: status flips to pending once the job lands.
      queryClient.invalidateQueries({ queryKey: ['repository', repositoryId] })
    },
  })

  const handleBack = () => {
    // If navigation happened inside the app, go back to keep search query and filters
    if (location.key !== 'default') {
      navigate(-1)
    } else {
      navigate('/search')
    }
  }

  if (query.isPending) {
    return (
      <div className="space-y-4">
        <PageHeader title="Repository Details" />
        <div className="flex flex-col items-center justify-center rounded-xl border p-12 text-center text-sm text-muted-foreground">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          <p className="mt-3">Loading indexed repository…</p>
        </div>
      </div>
    )
  }

  if (query.isError || !query.data) {
    return (
      <div className="space-y-4">
        <PageHeader title="Repository Details" />
        <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-semibold text-destructive">Repository could not be loaded.</p>
          <Button variant="outline" size="sm" className="mt-4" onClick={() => query.refetch()}>
            Try again
          </Button>
        </div>
      </div>
    )
  }

  const repo = query.data
  const enrichmentLabel = enrichmentLabelOf(repo.enrichment.status)
  const canRefresh =
    repo.enrichment.status === 'None' ||
    repo.enrichment.status === 'Complete' ||
    repo.enrichment.status === 'Failed'

  const ecosystemQuery = repo.topics[0] || repo.primaryLanguage || repo.name

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={handleBack}
          className="inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors cursor-pointer"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          <span>Back to previous page</span>
        </button>

        <div className="flex items-center gap-2">
          <EvidenceBadge
            type="deterministic"
            label="Indexed Repository"
            subtext="Persisted in PostgreSQL database with pgvector embedding"
          />
        </div>
      </div>

      {/* Header Info */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start justify-between rounded-xl border bg-card p-6 shadow-sm">
        <div className="space-y-2 max-w-2xl">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-bold tracking-tight text-foreground">{repo.fullName}</h1>
            {repo.isArchived && (
              <Badge variant="secondary" className="gap-1 text-xs">
                <Archive className="h-3 w-3" />
                archived
              </Badge>
            )}
            {repo.isFork && <Badge variant="outline" className="text-xs">fork</Badge>}
            <a
              href={repo.htmlUrl}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-medium text-muted-foreground hover:text-foreground hover:border-foreground/30 transition-colors"
            >
              <span>GitHub</span>
              <ExternalLink className="h-3 w-3" />
            </a>
          </div>

          <p className="text-sm text-muted-foreground leading-relaxed">
            {repo.description ?? 'No repository description provided.'}
          </p>

          <div className="flex flex-wrap gap-x-5 gap-y-2 pt-2 text-xs text-muted-foreground">
            <span className="inline-flex items-center gap-1 font-semibold text-foreground">
              <Star className="h-3.5 w-3.5 text-amber-500 fill-amber-500" />{' '}
              {repo.stars.toLocaleString()}
            </span>
            <span className="inline-flex items-center gap-1">
              <GitFork className="h-3.5 w-3.5" /> {repo.forks.toLocaleString()} forks
            </span>
            <span>{repo.openIssues.toLocaleString()} open issues</span>
            <span className="inline-flex items-center gap-1">
              <CalendarDays className="h-3.5 w-3.5" /> Created {formatDate(repo.createdAtUtc)}
            </span>
            <span>Updated {formatDate(repo.updatedAtUtc)}</span>
            {repo.licenseSpdx && <span>License: {repo.licenseSpdx}</span>}
            {repo.defaultBranch && <span>Branch: {repo.defaultBranch}</span>}
          </div>
        </div>

        {/* Action Buttons for Decision Journey */}
        <div className="flex flex-col gap-2 shrink-0 sm:w-60">
          <Link
            to={`/ecosystem?query=${encodeURIComponent(ecosystemQuery)}`}
            className="inline-flex items-center justify-between rounded-lg bg-primary px-3.5 py-2 text-xs font-semibold text-primary-foreground shadow-xs hover:bg-primary/90 transition-colors"
          >
            <div className="flex items-center gap-1.5">
              <Network className="h-3.5 w-3.5" />
              <span>Explore Ecosystem</span>
            </div>
            <ArrowRight className="h-3 w-3" />
          </Link>

          <Link
            to={`/validate?idea=${encodeURIComponent(`An alternative to ${repo.fullName} that provides `)}`}
            className="inline-flex items-center justify-between rounded-lg border bg-background px-3.5 py-2 text-xs font-medium text-foreground hover:bg-muted/50 transition-colors"
          >
            <div className="flex items-center gap-1.5">
              <Lightbulb className="h-3.5 w-3.5" />
              <span>Validate Competing Idea</span>
            </div>
            <ArrowRight className="h-3 w-3" />
          </Link>
        </div>
      </div>

      {/* Grid: Enrichment Status + Languages + Topics */}
      <div className="grid gap-4 md:grid-cols-3">
        {/* Enrichment Status */}
        <section className="rounded-xl border bg-card p-5 shadow-sm space-y-3 md:col-span-1">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-bold text-foreground">Enrichment State</h2>
            {canRefresh && (
              <Button
                variant="ghost"
                size="sm"
                className="h-7 gap-1 px-2 text-xs"
                disabled={refresh.isPending}
                onClick={() => refresh.mutate()}
                title="Trigger background worker re-enrichment"
              >
                <RefreshCw className={`h-3 w-3 ${refresh.isPending ? 'animate-spin' : ''}`} />
                <span>Refresh</span>
              </Button>
            )}
          </div>

          <div className="flex items-center gap-2">
            <span className={`h-2.5 w-2.5 rounded-full ${dotColorOf(repo.enrichment.status)}`} />
            <span className="text-xs font-semibold text-foreground">{enrichmentLabel}</span>
            {repo.enrichment.attempts > 0 && (
              <span className="text-[11px] text-muted-foreground">
                (attempt {repo.enrichment.attempts})
              </span>
            )}
          </div>

          {repo.enrichment.status === 'Failed' && repo.enrichment.lastError && (
            <p className="text-xs text-destructive bg-destructive/10 p-2 rounded">
              {repo.enrichment.lastError}
            </p>
          )}

          {repo.enrichment.status === 'Pending' && (
            <p className="text-xs text-muted-foreground leading-relaxed">
              Queued for background enrichment worker &mdash; README, topics, and pgvector embeddings are being generated.
            </p>
          )}

          {repo.enrichment.status === 'Complete' && repo.enrichedAtUtc && (
            <p className="text-[11px] text-muted-foreground">
              Last enriched {formatDateTime(repo.enrichedAtUtc)}
            </p>
          )}

          {refresh.isError && (
            <p className="text-xs text-destructive">
              {refresh.error instanceof RefreshError
                ? refresh.error.message
                : 'Could not schedule refresh.'}
            </p>
          )}
        </section>

        {/* Languages */}
        <section className="rounded-xl border bg-card p-5 shadow-sm space-y-3 md:col-span-1">
          <div className="flex items-center gap-1.5">
            <Code2 className="h-4 w-4 text-primary" />
            <h2 className="text-sm font-bold text-foreground">Languages</h2>
          </div>

          {repo.languages.length === 0 ? (
            <p className="text-xs text-muted-foreground py-2">No language bytes recorded.</p>
          ) : (
            <div className="space-y-2">
              {repo.languages.slice(0, 5).map((lang) => {
                const pct = Math.round(lang.percentage)
                return (
                  <div key={lang.language} className="space-y-1">
                    <div className="flex justify-between text-xs">
                      <span className="font-medium text-foreground">{lang.language}</span>
                      <span className="text-muted-foreground">{pct}%</span>
                    </div>
                    <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
                      <div
                        className="h-full bg-primary/70"
                        style={{ width: `${Math.min(100, Math.max(0, pct))}%` }}
                      />
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </section>

        {/* Topics */}
        <section className="rounded-xl border bg-card p-5 shadow-sm space-y-3 md:col-span-1">
          <h2 className="text-sm font-bold text-foreground">Topics & Taxonomy</h2>
          {repo.topics.length === 0 ? (
            <p className="text-xs text-muted-foreground py-2">No repository topics tagged.</p>
          ) : (
            <div className="flex flex-wrap gap-1.5">
              {repo.topics.map((topic) => (
                <Link
                  key={topic}
                  to={`/ecosystem?query=${encodeURIComponent(topic)}`}
                  className="rounded-md border bg-muted/30 px-2 py-0.5 text-xs text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
                  title={`Explore ecosystem around ${topic}`}
                >
                  #{topic}
                </Link>
              ))}
            </div>
          )}
        </section>
      </div>

      {/* Similar Projects Section */}
      <SimilarProjectsSection
        repositoryId={repo.id}
        enabled={repo.enrichment.status === 'Complete'}
      />

      {/* Normalized README Preview */}
      {repo.readme?.present && repo.readme.text && (
        <section className="rounded-xl border bg-card p-6 shadow-sm space-y-3">
          <div className="flex items-center justify-between border-b pb-3">
            <h2 className="text-base font-bold text-foreground">README Content (Normalized)</h2>
            <span className="text-xs text-muted-foreground">
              {repo.readme.text.length.toLocaleString()} characters
              {repo.readme.truncated ? ' (truncated)' : ''}
            </span>
          </div>
          <div className="max-h-96 overflow-y-auto rounded-lg bg-muted/20 p-4 font-mono text-xs text-foreground leading-relaxed whitespace-pre-wrap">
            {repo.readme.text}
          </div>
        </section>
      )}
    </div>
  )
}

function enrichmentLabelOf(status: string): string {
  switch (status) {
    case 'Complete':
      return 'Enriched & Indexed'
    case 'Pending':
      return 'Enrichment Queued'
    case 'Failed':
      return 'Enrichment Failed'
    default:
      return 'Not Enriched'
  }
}

function dotColorOf(status: string): string {
  switch (status) {
    case 'Complete':
      return 'bg-emerald-500'
    case 'Pending':
      return 'bg-amber-500 animate-pulse'
    case 'Failed':
      return 'bg-destructive'
    default:
      return 'bg-muted-foreground/40'
  }
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString()
  } catch {
    return iso
  }
}

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString()
  } catch {
    return iso
  }
}
