import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import {
  Archive,
  ArrowLeft,
  CalendarDays,
  Code2,
  ExternalLink,
  GitFork,
  RefreshCw,
  Star,
} from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { RefreshError, fetchRepositoryDetail, refreshRepository } from '@/lib/repositories'

export function RepositoryDetailPage() {
  const { repositoryId } = useParams()
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

  if (query.isPending) {
    return (
      <div>
        <PageHeader title="Repository" />
        <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">Loading…</div>
      </div>
    )
  }

  if (query.isError || !query.data) {
    return (
      <div>
        <PageHeader title="Repository" />
        <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-medium text-destructive">Repository could not be loaded.</p>
          <Button variant="outline" size="sm" className="mt-4" onClick={() => query.refetch()}>
            Try again
          </Button>
        </div>
      </div>
    )
  }

  const repo = query.data
  const enrichmentLabel = enrichmentLabelOf(repo.enrichment.status)
  const canRefresh = repo.enrichment.status === 'None'
    || repo.enrichment.status === 'Complete'
    || repo.enrichment.status === 'Failed'

  return (
    <div>
      <Link
        to="/discover"
        className="mb-4 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft data-slot="icon" className="size-3.5" />
        Back to search
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight">{repo.fullName}</h1>
            {repo.isArchived && (
              <Badge variant="secondary" className="gap-1">
                <Archive data-slot="icon" />
                archived
              </Badge>
            )}
            {repo.isFork && <Badge variant="outline">fork</Badge>}
            <a href={repo.htmlUrl} target="_blank" rel="noreferrer" aria-label="Open on GitHub">
              <Badge variant="outline" className="gap-1">
                GitHub <ExternalLink data-slot="icon" />
              </Badge>
            </a>
          </div>
          <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
            {repo.description ?? 'No description.'}
          </p>
        </div>
      </div>

      <div className="mt-4 flex flex-wrap gap-x-6 gap-y-2 text-sm text-muted-foreground">
        <span className="inline-flex items-center gap-1">
          <Star data-slot="icon" className="text-amber-500" /> {repo.stars.toLocaleString()}
        </span>
        <span className="inline-flex items-center gap-1">
          <GitFork data-slot="icon" /> {repo.forks.toLocaleString()}
        </span>
        <span>{repo.openIssues.toLocaleString()} open issues</span>
        <span className="inline-flex items-center gap-1">
          <CalendarDays data-slot="icon" className="size-4" /> Created{' '}
          {formatDate(repo.createdAtUtc)}
        </span>
        <span>Updated {formatDate(repo.updatedAtUtc)}</span>
        {repo.licenseSpdx && <span>License: {repo.licenseSpdx}</span>}
        {repo.defaultBranch && <span>Branch: {repo.defaultBranch}</span>}
      </div>

      <div className="mt-5 grid gap-4 md:grid-cols-3">
        <section className="rounded-lg border p-4 md:col-span-1">
          <h2 className="text-sm font-medium">Enrichment</h2>
          <div className="mt-2 flex items-center gap-2">
            <span className={`h-2 w-2 rounded-full ${dotColorOf(repo.enrichment.status)}`} />
            <span className="text-sm">{enrichmentLabel}</span>
            {repo.enrichment.attempts > 0 && (
              <span className="text-xs text-muted-foreground">
                attempt {repo.enrichment.attempts}
              </span>
            )}
          </div>
          {repo.enrichment.status === 'Failed' && repo.enrichment.lastError && (
            <p className="mt-2 text-xs text-destructive">{repo.enrichment.lastError}</p>
          )}
          {repo.enrichment.status === 'Pending' && (
            <p className="mt-2 text-xs text-muted-foreground">
              Queued — metadata, README, topics and languages are being collected.
            </p>
          )}
          {repo.enrichment.status === 'Complete' && repo.enrichedAtUtc && (
            <p className="mt-2 text-xs text-muted-foreground">
              Enriched {formatDateTime(repo.enrichedAtUtc)}
            </p>
          )}
          {repo.enrichment.status === 'Failed' && (
            <p className="mt-2 text-xs text-muted-foreground">The last enrichment run failed.</p>
          )}
          {repo.enrichment.status === 'None' && (
            <p className="mt-2 text-xs text-muted-foreground">Not enriched yet.</p>
          )}
          <Button
            size="sm"
            className="mt-3 gap-1.5"
            disabled={!canRefresh || refresh.isPending}
            onClick={() => refresh.mutate()}
          >
            <RefreshCw data-slot="icon" className={refresh.isPending ? 'animate-spin' : ''} />
            {refresh.isPending ? 'Queuing…' : 'Refresh repository'}
          </Button>
          {refresh.isError && refresh.error instanceof RefreshError && (
            <p className="mt-2 text-xs text-destructive">{refresh.error.message}</p>
          )}
          {refresh.isSuccess && (
            <p className="mt-2 text-xs text-muted-foreground">Refresh queued.</p>
          )}
        </section>

        <section className="rounded-lg border p-4 md:col-span-2">
          <h2 className="text-sm font-medium">Languages</h2>
          {repo.languages.length === 0 ? (
            <p className="mt-2 text-sm text-muted-foreground">
              Language breakdown unavailable — enrich this repository.
            </p>
          ) : (
            <ul className="mt-3 space-y-2">
              {repo.languages.slice(0, 8).map((language) => (
                <li key={language.language}>
                  <div className="flex justify-between text-xs">
                    <span>{language.language}</span>
                    <span className="text-muted-foreground">
                      {language.percentage.toLocaleString(undefined, { maximumFractionDigits: 1 })}%
                    </span>
                  </div>
                  <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
                    <div
                      className="h-full rounded-full bg-primary/70"
                      style={{ width: `${Math.min(language.percentage, 100)}%` }}
                    />
                  </div>
                </li>
              ))}
            </ul>
          )}

          {repo.topics.length > 0 && (
            <>
              <h2 className="mt-5 text-sm font-medium">Topics</h2>
              <div className="mt-2 flex flex-wrap gap-1.5">
                {repo.topics.map((topic) => (
                  <Badge key={topic} variant="secondary">
                    {topic}
                  </Badge>
                ))}
              </div>
            </>
          )}
        </section>
      </div>

      <section className="mt-4 rounded-lg border p-4">
        <h2 className="text-sm font-medium">README</h2>
        {repo.readme?.text ? (
          <>
            <pre className="mt-3 max-h-[40rem] overflow-auto whitespace-pre-wrap text-sm leading-relaxed">
              {repo.readme.text}
            </pre>
            {repo.readme.truncated && (
              <p className="mt-2 text-xs text-muted-foreground">
                README truncated for display — full content is used for analysis.
              </p>
            )}
          </>
        ) : (
          <p className="mt-2 text-sm text-muted-foreground">
            {repo.enrichment.status === 'Complete'
              ? 'This repository has no README.'
              : 'README will appear here after enrichment.'}
          </p>
        )}
      </section>

      <p className="mt-4 flex items-center gap-1 text-xs text-muted-foreground">
        <Code2 data-slot="icon" className="size-3.5" />
        RepoLens id {repo.id} · GitHub id {repo.gitHubId} · Discovered{' '}
        {formatDateTime(repo.discoveredAtUtc)}
      </p>
    </div>
  )
}

function enrichmentLabelOf(status: string): string {
  switch (status) {
    case 'Pending':
      return 'Pending'
    case 'Processing':
      return 'Processing'
    case 'Complete':
      return 'Complete'
    case 'Failed':
      return 'Failed'
    default:
      return 'Not enriched'
  }
}

function dotColorOf(status: string): string {
  switch (status) {
    case 'Pending':
    case 'Processing':
      return 'bg-amber-500'
    case 'Complete':
      return 'bg-emerald-500'
    case 'Failed':
      return 'bg-destructive'
    default:
      return 'bg-muted-foreground/40'
  }
}

function formatDate(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function formatDateTime(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      })
}
