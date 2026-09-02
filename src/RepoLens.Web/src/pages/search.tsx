import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Archive, Search } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
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
    <div>
      <PageHeader
        title="Search RepoLens"
        subtitle="Search repositories stored in RepoLens' own index — full-text and, when configured, semantic vectors merged with reciprocal rank fusion."
      />

      <form
        onSubmit={(e) => submit(1, e)}
        className="mb-4 flex flex-col gap-2"
        role="search"
      >
        <div className="flex flex-col gap-2 sm:flex-row">
          <input
            value={queryInput}
            onChange={(e) => setQueryInput(e.target.value)}
            placeholder="Search indexed repositories…"
            aria-label="Search query"
            className="h-9 flex-1 rounded-lg border bg-background px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          />
          <Button type="submit" disabled={mutation.isPending} className="gap-1.5">
            <Search data-slot="icon" />
            {mutation.isPending ? 'Searching…' : 'Search'}
          </Button>
        </div>
        <div className="flex flex-wrap items-center gap-3 text-sm">
          <label className="flex items-center gap-2">
            <span className="text-muted-foreground">Language</span>
            <input
              value={language}
              onChange={(e) => setLanguage(e.target.value)}
              placeholder="e.g. Go"
              className="h-8 w-32 rounded-lg border bg-background px-2 text-sm outline-none"
            />
          </label>
          <label className="flex items-center gap-2">
            <span className="text-muted-foreground">Min stars</span>
            <input
              value={minStars}
              onChange={(e) => setMinStars(e.target.value)}
              inputMode="numeric"
              placeholder="0"
              className="h-8 w-24 rounded-lg border bg-background px-2 text-sm outline-none"
            />
          </label>
          <label className="flex cursor-pointer items-center gap-2">
            <input
              type="checkbox"
              checked={excludeArchived}
              onChange={(e) => setExcludeArchived(e.target.checked)}
              className="size-4 accent-primary"
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
}: {
  isPending: boolean
  error: Error | null
  result: RepositorySearchResponse | null
  onRetry: () => void
  canGoBack: boolean
  canGoForward: boolean
  onGoToPage: (page: number, e?: React.FormEvent) => void
}) {
  if (isPending) {
    return (
      <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">Searching…</div>
    )
  }

  if (error) {
    const message = error instanceof ApiError ? error.message : 'Something went wrong.'
    return (
      <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-6" role="alert">
        <p className="text-sm font-medium text-destructive">Search failed</p>
        <p className="mt-1 text-sm text-muted-foreground">{message}</p>
        <Button variant="outline" size="sm" className="mt-4" onClick={onRetry}>
          Try again
        </Button>
      </div>
    )
  }

  if (!result) {
    return (
      <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">
        Run a search over the repositories RepoLens has enriched.
      </div>
    )
  }

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
        <Badge variant="secondary">{result.method} search</Badge>
        <span>
          {result.totalCount} result{result.totalCount === 1 ? '' : 's'} for “{result.query}”
        </span>
        {result.vectorFallbackReason && <span>· semantic leg unavailable ({result.vectorFallbackReason})</span>}
      </div>

      {result.items.length === 0 ? (
        <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">
          No indexed repositories matched. Enriched repositories are indexed automatically.
        </div>
      ) : (
        <ul className="flex flex-col gap-2" aria-label="Search results">
          {result.items.map((repo) => (
            <li key={repo.id} className="rounded-lg border bg-background p-4 transition-colors hover:bg-muted/30">
              <div className="flex flex-wrap items-center gap-2">
                <Link to={`/repositories/${repo.id}`} className="font-medium hover:underline">
                  {repo.fullName}
                </Link>
                {repo.isArchived && (
                  <Badge variant="secondary" className="gap-1">
                    <Archive data-slot="icon" /> archived
                  </Badge>
                )}
                {repo.primaryLanguage && <Badge variant="outline">{repo.primaryLanguage}</Badge>}
                <span className="ml-auto text-sm text-muted-foreground">
                  {repo.stars.toLocaleString()} stars
                </span>
              </div>
              {repo.description && (
                <p className="mt-1.5 line-clamp-2 text-sm text-muted-foreground">{repo.description}</p>
              )}
              <div className="mt-2">
                <div className="h-1 w-full max-w-64 overflow-hidden rounded-full bg-muted">
                  <div
                    className="h-full rounded-full bg-primary/70"
                    style={{ width: `${Math.round(repo.score * 100)}%` }}
                  />
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}

      {(canGoBack || canGoForward) && (
        <div className="mt-4 flex items-center gap-2">
          <Button variant="outline" size="sm" disabled={!canGoBack} onClick={() => onGoToPage(result.page - 1)}>
            Previous
          </Button>
          <span className="text-sm tabular-nums text-muted-foreground">Page {result.page}</span>
          <Button variant="outline" size="sm" disabled={!canGoForward} onClick={() => onGoToPage(result.page + 1)}>
            Next
          </Button>
        </div>
      )}
    </div>
  )
}

