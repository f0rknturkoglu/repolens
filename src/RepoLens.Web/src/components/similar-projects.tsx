import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Archive, GitCompare } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { fetchSimilarRepositories } from '@/lib/search'

/** “Similar Projects” — semantically close repositories via stored embeddings. */
export function SimilarProjectsSection({ repositoryId, enabled }: { repositoryId: number; enabled: boolean }) {
  const query = useQuery({
    queryKey: ['similar', repositoryId],
    queryFn: ({ signal }) => fetchSimilarRepositories(repositoryId, signal),
    enabled: enabled && repositoryId > 0,
    retry: false,
  })

  return (
    <section className="mt-4 rounded-lg border p-4">
      <h2 className="flex items-center gap-1.5 text-sm font-medium">
        <GitCompare data-slot="icon" className="size-4" />
        Similar Projects
      </h2>
      <div className="mt-3">
        {!enabled ? (
          <p className="text-sm text-muted-foreground">
            Similarity needs enrichment — refresh this repository first.
          </p>
        ) : query.isPending ? (
          <p className="text-sm text-muted-foreground">Comparing embeddings…</p>
        ) : query.isError ? (
          <p className="text-sm text-muted-foreground">Similar projects are unavailable right now.</p>
        ) : query.data.status !== 'ok' ? (
          <p className="text-sm text-muted-foreground">
            {query.data.status === 'unavailable'
              ? query.data.reason ?? 'Semantic search is not configured.'
              : 'No similar projects found.'}
          </p>
        ) : query.data.items.length === 0 ? (
          <p className="text-sm text-muted-foreground">No close matches in the indexed dataset.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {query.data.items.map((item) => (
              <li key={item.id} className="rounded-md border p-3">
                <div className="flex flex-wrap items-center gap-2">
                  <Link to={`/repositories/${item.id}`} className="text-sm font-medium hover:underline">
                    {item.fullName}
                  </Link>
                  {item.isArchived && (
                    <Badge variant="secondary" className="gap-1">
                      <Archive data-slot="icon" /> archived
                    </Badge>
                  )}
                  <span className="ml-auto text-xs text-muted-foreground">
                    {item.stars.toLocaleString()} stars
                  </span>
                </div>
                {item.description && (
                  <p className="mt-1 line-clamp-1 text-xs text-muted-foreground">{item.description}</p>
                )}
                {item.evidence.length > 0 && (
                  <ul className="mt-1.5 flex flex-col gap-0.5 text-xs text-muted-foreground">
                    {item.evidence.map((line) => (
                      <li key={line}>· {line}</li>
                    ))}
                  </ul>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
