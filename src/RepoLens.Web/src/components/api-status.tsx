import { useQuery } from '@tanstack/react-query'
import { Badge } from '@/components/ui/badge'
import { fetchApiHealth, type ApiHealthState } from '@/lib/health'

/**
 * Polls GET /health through TanStack Query and renders the API state as a badge.
 * "Healthy" is only shown while the backend (and, by extension, PostgreSQL via
 * the database health check) is reachable.
 */
export function ApiStatusBadge() {
  const { data, isError } = useQuery({
    queryKey: ['api-health'],
    queryFn: ({ signal }) => fetchApiHealth(signal),
    refetchInterval: 15_000,
    staleTime: 10_000,
    retry: 1,
  })

  // On error the last successful value may still be cached; a failed poll must
  // surface as Unavailable, so the error state wins over stale data.
  const state: ApiHealthState =
    isError || data === 'unavailable' ? 'unavailable' : data === 'healthy' ? 'healthy' : 'checking'

  if (state === 'healthy') {
    return (
      <Badge className="bg-emerald-600 text-white" aria-label="API status: healthy">
        Healthy
      </Badge>
    )
  }

  if (state === 'unavailable') {
    return (
      <Badge variant="destructive" aria-label="API status: unavailable">
        Unavailable
      </Badge>
    )
  }

  return (
    <Badge variant="secondary" aria-label="API status: checking">
      Checking…
    </Badge>
  )
}
