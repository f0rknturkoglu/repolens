import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  Archive,
  Search,
  ExternalLink,
  Compass,
  Network,
  Lightbulb,
  ArrowRight,
  Database,
} from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { EvidenceBadge } from '@/components/evidence-badge'
import { ApiError } from '@/lib/api'
import { searchRepoLens, type RepositorySearchResponse } from '@/lib/search'

export function SearchPage() {
  const [queryInput, setQueryInput] = useState('')
  const [language, setLanguage] = useState('')
  const [minStars, setMinStars] = useState('')
  const [excludeArchived, setExcludeArchived] = useState(false)
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<RepositorySearchResponse | null>(null)

  const mutation = useMutation({
    mutationFn: ({ query, page }: { query: string; page: number }) =>
      searchRepoLens(
        query,
        {
          language: language.trim() || undefined,
          archived: excludeArchived ? false : undefined,
          minStars: minStars.trim() ? Number(minStars.trim()) : undefined,
        },
        page,
      ),
    onSuccess: (data) => setResult(data),
  })

  const submit = (nextPage: number, e?: React.FormEvent) => {
    e?.preventDefault()
    const query = queryInput.trim()
    if (!query) return
    setPage(nextPage)
    mutation.mutate({ query, page: nextPage })
  }

  const canGoBack = page > 1
  const canGoForward = result !== null && page * 20 < result.totalCount

  return (
    <div className="space-y-6">
      <PageHeader
        title="Search RepoLens Index"
        subtitle="Search repositories stored in the local PostgreSQL index using full-text search and pgvector embeddings merged with Reciprocal Rank Fusion (k=60)."
      />

      {/* Distinction Banner: Local Index vs Live GitHub */}
      <div className="flex items-start justify-between gap-4 rounded-xl border bg-muted/20 p-4 text-xs text-muted-foreground">
        <div className="flex items-center gap-2">
          <Database className="h-4 w-4 text-primary shrink-0" />
          <span>
            This searches your <strong>local enriched repository database</strong>.
          </span>
        </div>
        <Link
          to={`/discover${queryInput.trim() ? `?q=${encodeURIComponent(queryInput.trim())}` : ''}`}
          className="inline-flex items-center gap-1 font-semibold text-primary hover:underline shrink-0"
        >
          <span>Search Live GitHub instead</span>
          <ArrowRight className="h-3 w-3" />
        </Link>
      </div>

      <form
        onSubmit={(e) => submit(1, e)}
        className="flex flex-col gap-4 rounded-xl border bg-card p-5 shadow-sm"
        role="search"
      >
        <div className="flex flex-col gap-2 sm:flex-row">
          <input
            value={queryInput}
            onChange={(e) => setQueryInput(e.target.value)}
            placeholder="Search indexed repositories by keyword or semantic concept…"
            aria-label="Search query"
            className="h-10 flex-1 rounded-lg border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-primary"
          />
          <Button type="submit" disabled={mutation.isPending} className="gap-2 shrink-0">
            <Search className="h-4 w-4" />
            {mutation.isPending ? 'Searching Index…' : 'Search Index'}
          </Button>
        </div>

        <div className="flex flex-wrap items-center gap-4 text-xs">
          <label className="flex items-center gap-2">
            <span className="text-muted-foreground font-medium">Language:</span>
            <input
              value={language}
              onChange={(e) => setLanguage(e.target.value)}
              placeholder="e.g. Go, Rust, C#"
              className="h-8 w-28 rounded-lg border bg-background px-2.5 text-xs outline-none focus-visible:ring-1 focus-visible:ring-primary"
            />
          </label>

          <label className="flex items-center gap-2">
            <span className="text-muted-foreground font-medium">Min Stars:</span>
            <input
              value={minStars}
              onChange={(e) => setMinStars(e.target.value)}
              inputMode="numeric"
              placeholder="0"
              className="h-8 w-20 rounded-lg border bg-background px-2.5 text-xs outline-none focus-visible:ring-1 focus-visible:ring-primary"
            />
          </label>

          <label className="flex cursor-pointer items-center gap-2">
            <input
              type="checkbox"
              checked={excludeArchived}
              onChange={(e) => setExcludeArchived(e.target.checked)}
              className="size-4 rounded border-gray-300 accent-primary"
            />
            <span className="text-muted-foreground">Exclude archived</span>
          </label>
        </div>
      </form>

      <StateContent
        isPending={mutation.isPending}
        error={mutation.error}
        result={result}
        onRetry={() => submit(page)}
        canGoBack={canGoBack}
        canGoForward={canGoForward}
        onGoToPage={submit}
        query={queryInput.trim()}
      />
    </div>
  )
}

function StateContent({
  isPending,
  error,
  result,
  onRetry,
  canGoBack,
  canGoForward,
  onGoToPage,
  query,
}: {
  isPending: boolean
  error: Error | null
  result: RepositorySearchResponse | null
  onRetry: () => void
  canGoBack: boolean
  canGoForward: boolean
  onGoToPage: (page: number, e?: React.FormEvent) => void
  query: string
}) {
  if (isPending) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed p-12 text-center text-sm text-muted-foreground">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        <p>Executing hybrid ranking over local PostgreSQL index…</p>
      </div>
    )
  }

  if (error) {
    const message = error instanceof ApiError ? error.message : 'Something went wrong.'
    return (
      <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6" role="alert">
        <p className="text-sm font-semibold text-destructive">Search failed</p>
        <p className="mt-1 text-xs text-muted-foreground">{message}</p>
        <Button variant="outline" size="sm" className="mt-4" onClick={onRetry}>
          Try again
        </Button>
      </div>
    )
  }

  if (!result) {
    return (
      <div className="rounded-xl border border-dashed p-10 text-center text-xs text-muted-foreground space-y-2">
        <Search className="mx-auto h-8 w-8 opacity-40" />
        <p className="font-medium text-foreground text-sm">Search local repository index</p>
        <p className="max-w-md mx-auto">
          Enriched repositories in RepoLens are indexed with full-text tokens and vector embeddings for instant semantic search.
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      {/* Results Header with EvidenceBadges */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/20 px-4 py-2.5 text-xs text-muted-foreground">
        <div className="flex items-center gap-2">
          {result.method === 'Hybrid' ? (
            <EvidenceBadge
              type="hybrid"
              label="Hybrid RRF (k=60)"
              subtext="Combines PostgreSQL tsvector rank and pgvector cosine distance"
            />
          ) : (
            <EvidenceBadge
              type="deterministic"
              label="Full Text Search"
              subtext="PostgreSQL tsvector text search (embeddings unconfigured or degraded)"
            />
          )}
          <span className="font-semibold text-foreground">
            {result.totalCount} match{result.totalCount === 1 ? '' : 'es'}
          </span>
          <span>for &ldquo;{result.query}&rdquo;</span>
        </div>

        {result.vectorFallbackReason && (
          <span className="rounded bg-amber-500/10 px-2 py-0.5 text-[11px] text-amber-700 dark:text-amber-300">
            Semantic leg fallback: {result.vectorFallbackReason}
          </span>
        )}
      </div>

      {result.items.length === 0 ? (
        <div className="rounded-xl border border-dashed p-10 text-center text-xs text-muted-foreground space-y-3">
          <p className="font-medium text-foreground text-sm">No indexed repositories matched this query.</p>
          <p className="max-w-md mx-auto">
            RepoLens only indexes repositories that have been enriched locally. Try searching live GitHub to discover and enrich new repositories.
          </p>
          <div className="pt-2">
            <Link
              to={`/discover${query ? `?q=${encodeURIComponent(query)}` : ''}`}
              className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-3.5 py-1.5 text-xs font-semibold text-primary-foreground shadow-xs hover:bg-primary/90 transition-colors"
            >
              <Compass className="h-3.5 w-3.5" />
              <span>Search GitHub Live</span>
              <ArrowRight className="h-3 w-3" />
            </Link>
          </div>
        </div>
      ) : (
        <ul className="space-y-3" aria-label="Search results">
          {result.items.map((repo) => {
            const scorePct = Math.round(repo.score * 100)
            return (
              <li
                key={repo.id}
                className="rounded-xl border bg-card p-5 shadow-2xs space-y-3 hover:border-primary/40 transition-colors"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="space-y-1 max-w-2xl">
                    <div className="flex flex-wrap items-center gap-2">
                      <Link
                        to={`/repositories/${repo.id}`}
                        className="font-bold text-base text-foreground hover:text-primary transition-colors"
                      >
                        {repo.fullName}
                      </Link>
                      {repo.isArchived && (
                        <Badge variant="secondary" className="gap-1 text-[11px]">
                          <Archive className="h-3 w-3" /> archived
                        </Badge>
                      )}
                      {repo.primaryLanguage && (
                        <Badge variant="outline" className="text-[11px]">
                          {repo.primaryLanguage}
                        </Badge>
                      )}
                      <a
                        href={repo.htmlUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="text-muted-foreground hover:text-foreground p-0.5"
                        title="Open on GitHub"
                      >
                        <ExternalLink className="h-3.5 w-3.5" />
                      </a>
                    </div>

                    {repo.description && (
                      <p className="text-xs text-muted-foreground leading-relaxed line-clamp-2">
                        {repo.description}
                      </p>
                    )}
                  </div>

                  <div className="flex flex-col items-end gap-1 shrink-0">
                    <span className="font-mono text-xs font-semibold text-primary">
                      {scorePct}% relevance
                    </span>
                    <span className="text-xs text-muted-foreground tabular-nums">
                      {repo.stars.toLocaleString()}★
                    </span>
                  </div>
                </div>

                {/* Relevance meter */}
                <div className="h-1.5 w-full max-w-xs overflow-hidden rounded-full bg-muted">
                  <div
                    className="h-full bg-primary/70 transition-all"
                    style={{ width: `${Math.min(100, Math.max(0, scorePct))}%` }}
                  />
                </div>

                {/* Context Action Bridges */}
                <div className="pt-2 border-t flex flex-wrap items-center justify-between gap-2 text-xs">
                  <Link
                    to={`/repositories/${repo.id}`}
                    className="font-semibold text-primary hover:underline inline-flex items-center gap-1"
                  >
                    <span>Inspect Enrichment & Similar Repos</span>
                    <ArrowRight className="h-3 w-3" />
                  </Link>

                  <div className="flex items-center gap-3 text-muted-foreground">
                    <Link
                      to={`/ecosystem?query=${encodeURIComponent(repo.name)}`}
                      className="inline-flex items-center gap-1 hover:text-foreground"
                    >
                      <Network className="h-3 w-3" />
                      <span>Ecosystem</span>
                    </Link>
                    <Link
                      to={`/validate?idea=${encodeURIComponent(`An alternative to ${repo.fullName}`)}`}
                      className="inline-flex items-center gap-1 hover:text-foreground"
                    >
                      <Lightbulb className="h-3 w-3" />
                      <span>Validate Idea</span>
                    </Link>
                  </div>
                </div>
              </li>
            )
          })}
        </ul>
      )}

      {(canGoBack || canGoForward) && (
        <div className="mt-4 flex items-center justify-center gap-3">
          <Button
            variant="outline"
            size="sm"
            disabled={!canGoBack}
            onClick={() => onGoToPage(result.page - 1)}
          >
            Previous
          </Button>
          <span className="text-xs tabular-nums text-muted-foreground font-medium">
            Page {result.page}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={!canGoForward}
            onClick={() => onGoToPage(result.page + 1)}
          >
            Next
          </Button>
        </div>
      )}
    </div>
  )
}
