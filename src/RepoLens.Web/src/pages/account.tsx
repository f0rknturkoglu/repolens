import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { History, LogIn, LogOut, UserRound } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { ApiError } from '@/lib/api'
import { fetchAuthStatus, fetchHistory, historyPath, type HistoryEntry } from '@/lib/account'

export function AccountPage() {
  const [signedOut, setSignedOut] = useState(false)
  const status = useQuery({ queryKey: ['auth-status'], queryFn: ({ signal }) => fetchAuthStatus(signal) })
  const user = status.data?.user
  const history = useQuery({
    queryKey: ['history', user?.userId],
    queryFn: ({ signal }) => fetchHistory(signal),
    enabled: user != null && !signedOut,
  })

  useEffect(() => {
    if (signedOut) {
      setSignedOut(false)
      status.refetch()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [signedOut])

  return (
    <div>
      <PageHeader
        title="Account & history"
        subtitle="Signed-in users get saved analysis history. Everything also works anonymously."
      />

      {status.isPending && <p className="text-sm text-muted-foreground">Loading…</p>}

      {status.isError && (
        <p className="text-sm text-destructive">
          {status.error instanceof ApiError ? status.error.message : 'Could not reach the API.'}
        </p>
      )}

      {status.data && !status.data.enabled && (
        <div className="rounded-lg border p-6">
          <p className="text-sm font-medium">GitHub sign-in is not configured</p>
          <p className="mt-1 text-sm text-muted-foreground">
            This installation runs without OAuth (set <code>Auth:CookieKey</code> and{' '}
            <code>GitHub:OAuthClientId/Secret</code> to enable it). Anonymous use is fully
            supported.
          </p>
        </div>
      )}

      {status.data?.enabled && !user && (
        <div className="rounded-lg border p-6">
          <p className="text-sm font-medium">Sign in with GitHub</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Used only for identity and saved analysis history — RepoLens requests the minimum
            scope and never accesses private repositories.
          </p>
          <Button className="mt-4 gap-1.5" onClick={() => (window.location.href = '/api/auth/login')}>
            <LogIn data-slot="icon" />
            Sign in with GitHub
          </Button>
        </div>
      )}

      {user && (
        <div>
          <div className="flex flex-wrap items-center gap-3 rounded-lg border p-4">
            {user.avatarUrl ? (
              <img src={user.avatarUrl} alt="" className="size-10 rounded-full" />
            ) : (
              <UserRound data-slot="icon" className="size-10 text-muted-foreground" />
            )}
            <div>
              <p className="font-medium">{user.name ?? user.login}</p>
              <p className="text-sm text-muted-foreground">@{user.login}</p>
            </div>
            <Button
              variant="outline"
              className="ml-auto gap-1.5"
              onClick={async () => {
                await fetch('/api/auth/logout', { method: 'POST' })
                setSignedOut(true)
              }}
            >
              <LogOut data-slot="icon" />
              Sign out
            </Button>
          </div>

          <section className="mt-4 rounded-lg border p-4">
            <h2 className="flex items-center gap-1.5 text-sm font-medium">
              <History data-slot="icon" className="size-4" />
              Saved analyses
            </h2>
            {history.isPending && <p className="mt-2 text-sm text-muted-foreground">Loading…</p>}
            {history.data && history.data.length === 0 && (
              <p className="mt-2 text-sm text-muted-foreground">
                No saved analyses yet — run an ecosystem or idea analysis while signed in.
              </p>
            )}
            {history.data && history.data.length > 0 && (
              <ul className="mt-3 flex flex-col gap-1.5">
                {history.data.map((entry: HistoryEntry) => {
                  const path = historyPath(entry)
                  const body = (
                    <span className="flex flex-wrap items-center gap-2">
                      <Badge variant="outline">{entry.kind}</Badge>
                      <span className="truncate">{entry.title}</span>
                      <Badge variant={entry.status === 'Completed' ? 'secondary' : 'destructive'}>
                        {entry.status}
                      </Badge>
                      <span className="ml-auto text-xs text-muted-foreground">
                        {formatDateTime(entry.createdAtUtc)} · v{entry.version}
                      </span>
                    </span>
                  )
                  return (
                    <li key={entry.id} className="rounded-md border px-3 py-2 text-sm hover:bg-muted/30">
                      {path.startsWith('/') && path !== '/' && !entry.title.startsWith('@') ? (
                        <Link to={path} className="block">
                          {body}
                        </Link>
                      ) : (
                        body
                      )}
                    </li>
                  )
                })}
              </ul>
            )}
          </section>
        </div>
      )}
    </div>
  )
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
