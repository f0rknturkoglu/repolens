import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Archive, Code2, ExternalLink, Search, Star } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { ApiError, searchRepositories, type DiscoveryResponse } from '@/lib/api'

export function DiscoverPage() {
  const [queryInput, setQueryInput] = useState('postgres migration')
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<DiscoveryResponse | null>(null)

  const mutation = useMutation({
    mutationFn: ({ query, page }: { query: string; page: number }) =>
      searchRepositories({ query, page }),
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
  const canGoForward = result !== null && result.items.length === result.pagination.pageSize

  return (
    <div>
      <PageHeader
        title="Explore GitHub"
        subtitle="Search GitHub repositories. Results are stored locally by RepoLens for enrichment and later analysis."
      />

      <form
        onSubmit={(e) => submit(1, e)}
        className="mb-4 flex flex-col gap-2 sm:flex-row"
        role="search"
      >
        <input
          value={queryInput}
          onChange={(e) => setQueryInput(e.target.value)}
          placeholder="e.g. postgres migration"
          aria-label="Search query"
          className="h-9 flex-1 rounded-lg border bg-background px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
        />
        <Button type="submit" disabled={mutation.isPending} className="gap-1.5">
          <Search data-slot="icon" />
          {mutation.isPending ? 'Searching…' : 'Search'}
        </Button>
      </form>

      <StateContent
        isPending={mutation.isPending}
        error={mutation.error}
        result={result}
        hasSearched={mutation.isSuccess || mutation.isError || result !== null}
        onRetry={() => submit(page)}
        onGoToPage={submit}
        canGoBack={canGoBack}
        canGoForward={canGoForward}
      />
    </div>
  )
}

function StateContent({
  isPending,
  error,
  result,
  hasSearched,
  onRetry,
  onGoToPage,
  canGoBack,
  canGoForward,
}: {
  isPending: boolean
  error: Error | null
  result: DiscoveryResponse | null
  hasSearched: boolean
  onRetry: () => void
  onGoToPage: (page: number, e?: React.FormEvent) => void
  canGoBack: boolean
  canGoForward: boolean
}) {
  if (isPending) {
    return (
      <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">
        Searching GitHub and storing results…
      </div>
    )
  }

  if (error) {
    const message = error instanceof ApiError ? error.message : 'Something went wrong.'
    const isRateLimited =
      error instanceof ApiError &&
      (error.status === 429 || error.code === 'github_rate_limited')
    return (
      <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-6" role="alert">
        <p className="text-sm font-medium text-destructive">
          {isRateLimited ? 'GitHub rate limit reached' : 'Search failed'}
        </p>
        <p className="mt-1 text-sm text-muted-foreground">{message}</p>
        <Button variant="outline" size="sm" className="mt-4" onClick={onRetry}>
          Try again
        </Button>
      </div>
    )
  }

  if (!result) {
    if (!hasSearched) {
      return (
        <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">
          Run a search to discover repositories.
        </div>
      )
    }
    return null
  }

  if (result.items.length === 0) {
    return (
      <div className="rounded-lg border p-8 text-center text-sm text-muted-foreground">
        No repositories matched “{result.query}”.
      </div>
    )
  }

  return (
    <div>
      <ul className="flex flex-col gap-2" aria-label="Search results">
        {result.items.map((repo) => (
          <li
            key={repo.gitHubId}
            className="rounded-lg border bg-background p-4 transition-colors hover:bg-muted/30"
          >
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <Link
                to={`/repositories/${repo.id}`}
                className="font-medium hover:underline"
              >
                {repo.fullName}
              </Link>
              <a href={repo.htmlUrl} target="_blank" rel="noreferrer" aria-label="Open on GitHub">
                <Badge variant="ghost" className="gap-1 text-muted-foreground">
                  <ExternalLink data-slot="icon" />
                </Badge>
              </a>
              {repo.isArchived && (
                <Badge variant="secondary" className="gap-1">
                  <Archive data-slot="icon" />
                  archived
                </Badge>
              )}
              {repo.isFork && <Badge variant="outline">fork</Badge>}
              {repo.primaryLanguage && (
                <Badge variant="outline" className="gap-1">
                  <Code2 data-slot="icon" />
                  {repo.primaryLanguage}
                </Badge>
              )}
              <span className="ml-auto inline-flex items-center gap-1 text-sm text-muted-foreground">
                <Star data-slot="icon" className="text-amber-500" />
                {repo.stars.toLocaleString()}
              </span>
            </div>
            {repo.description && (
              <p className="mt-1.5 line-clamp-2 text-sm text-muted-foreground">{repo.description}</p>
            )}
            <div className="mt-1.5 flex gap-4 text-xs text-muted-foreground">
              <span>Updated {formatDate(repo.updatedAtUtc)}</span>
              <span>{repo.forks.toLocaleString()} forks</span>
              <span>{repo.openIssues.toLocaleString()} open issues</span>
            </div>
          </li>
        ))}
      </ul>

      <div className="mt-4 flex flex-wrap items-center justify-between gap-2">
        <Pager
          canGoBack={canGoBack}
          canGoForward={canGoForward}
          page={result.pagination.page}
          onGoToPage={onGoToPage}
        />
        <p className="text-xs text-muted-foreground">
          {result.pagination.totalCount != null
            ? `${result.pagination.totalCount.toLocaleString()} total matches`
            : ''}
          {result.rateLimit
            ? ` · GitHub API ${result.rateLimit.remaining}/${result.rateLimit.limit} remaining`
            : ''}
        </p>
      </div>
    </div>
  )
}

function Pager({
  canGoBack,
  canGoForward,
  page,
  onGoToPage,
}: {
  canGoBack: boolean
  canGoForward: boolean
  page: number
  onGoToPage: (page: number, e?: React.FormEvent) => void
}) {
  return (
    <div className="flex items-center gap-2">
      <Button variant="outline" size="sm" disabled={!canGoBack} onClick={() => onGoToPage(page - 1)}>
        Previous
      </Button>
      <span className="text-sm tabular-nums text-muted-foreground">Page {page}</span>
      <Button variant="outline" size="sm" disabled={!canGoForward} onClick={() => onGoToPage(page + 1)}>
        Next
      </Button>
    </div>
  )
}

function formatDate(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
