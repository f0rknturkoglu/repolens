import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { FlaskConical, Lightbulb, Radar } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { ApiError, getJson } from '@/lib/api'
import { z } from 'zod'

const noveltySchema = z.object({
  score: z.number(),
  formulaVersion: z.string(),
  components: z.array(
    z.object({ key: z.string(), label: z.string(), value: z.number(), evidence: z.string() }),
  ),
  competitors: z.array(
    z.object({
      fullName: z.string(),
      primaryLanguage: z.string().nullable(),
      stars: z.number(),
      isArchived: z.boolean(),
      overlap: z.number(),
      reasons: z.array(z.string()),
    }),
  ),
})

const ideaValidationResponseSchema = z.object({
  id: z.number(),
  ideaText: z.string(),
  version: z.string(),
  status: z.string(),
  createdAtUtc: z.string(),
  completedAtUtc: z.string().nullable(),
  servedFromCache: z.boolean(),
  novelty: noveltySchema.nullable(),
  queryPlan: z.array(z.object({ text: z.string(), source: z.string() })).nullable(),
  clusters: z.array(z.object({ label: z.string(), memberCount: z.number(), topRepositories: z.array(z.string()) })).nullable(),
  evidence: z.array(z.string()).nullable(),
  gaps: z.array(z.object({ kind: z.string(), statement: z.string() })).nullable(),
  limitations: z.array(z.string()),
})

type IdeaValidationResponse = z.infer<typeof ideaValidationResponseSchema>

function validateIdea(idea: string, signal?: AbortSignal): Promise<IdeaValidationResponse> {
  return getJson('/api/analysis/idea', ideaValidationResponseSchema, signal, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ idea }),
  })
}

const SAMPLE_IDEA = 'A PostgreSQL migration load-testing laboratory that replays real schema-change workloads'

export function IdeaValidationPage() {
  const [idea, setIdea] = useState(SAMPLE_IDEA)
  const [result, setResult] = useState<IdeaValidationResponse | null>(null)

  const mutation = useMutation({
    mutationFn: (text: string) => validateIdea(text),
    onSuccess: (data) => setResult(data),
  })

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    const text = idea.trim()
    if (!text) return
    mutation.mutate(text)
  }

  const novelty = result?.novelty

  return (
    <div>
      <PageHeader
        title="Validate an Idea"
        subtitle="Describe a project idea; RepoLens measures how much of it already exists on GitHub — with evidence, competitors, and gaps observed in the analyzed set."
      />

      <form onSubmit={submit} className="flex flex-col gap-2">
        <textarea
          value={idea}
          onChange={(e) => setIdea(e.target.value)}
          rows={3}
          placeholder="Describe your project idea…"
          aria-label="Project idea"
          className="w-full resize-y rounded-lg border bg-background px-3 py-2 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
        />
        <div>
          <Button type="submit" disabled={mutation.isPending} className="gap-1.5">
            <Radar data-slot="icon" />
            {mutation.isPending ? 'Validating…' : 'Validate idea'}
          </Button>
        </div>
      </form>

      {mutation.isPending && (
        <div className="mt-4 rounded-lg border p-8 text-center text-sm text-muted-foreground">
          Searching GitHub for the idea’s landscape and scoring novelty…
        </div>
      )}

      {mutation.isError && (
        <div className="mt-4 rounded-lg border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-medium text-destructive">Validation failed</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {mutation.error instanceof ApiError ? mutation.error.message : 'Something went wrong.'}
          </p>
        </div>
      )}

      {result && result.status === 'failed' && (
        <div className="mt-4 rounded-lg border p-6">
          <p className="text-sm font-medium">Validation could not complete</p>
          <p className="mt-1 text-sm text-muted-foreground">
            GitHub could not be reached or returned no candidates.
          </p>
        </div>
      )}

      {result && result.status === 'completed' && novelty && (
        <div className="mt-4">
          <div className="mb-3 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            {result.servedFromCache && <Badge variant="secondary">cached (identical idea analyzed recently)</Badge>}
            <span>
              analysis #{result.id} · formula {novelty.formulaVersion}
            </span>
            <span>
              {result.queryPlan?.length ?? 0} search queries
            </span>
          </div>

          <section className="rounded-lg border p-4">
            <div className="flex flex-wrap items-center gap-4">
              <div className="flex items-center gap-3">
                <FlaskConical data-slot="icon" className="size-6 text-primary" />
                <div>
                  <p className="text-3xl font-semibold tabular-nums">
                    {Math.round(novelty.score)}
                    <span className="text-lg text-muted-foreground">/100</span>
                  </p>
                  <p className="text-xs text-muted-foreground">
                    Estimated Novelty — heuristic, not a market claim
                  </p>
                </div>
              </div>
              <Badge variant={noveltyBand(novelty.score)}>{noveltyBandLabel(novelty.score)}</Badge>
            </div>

            <ul className="mt-4 grid gap-2 md:grid-cols-2">
              {novelty.components.map((component) => (
                <li key={component.key} className="rounded-md border p-3 text-sm">
                  <div className="flex items-center justify-between">
                    <span className="font-medium">{component.label}</span>
                    <span className="tabular-nums text-muted-foreground">
                      {Math.round(component.value * 100)}%
                    </span>
                  </div>
                  <div className="mt-1.5 h-1 overflow-hidden rounded-full bg-muted">
                    <div
                      className="h-full rounded-full bg-primary/70"
                      style={{ width: `${Math.round(component.value * 100)}%` }}
                    />
                  </div>
                  <p className="mt-1.5 text-xs text-muted-foreground">{component.evidence}</p>
                </li>
              ))}
            </ul>
          </section>

          <section className="mt-4 rounded-lg border p-4">
            <h2 className="text-sm font-medium">Closest competitors</h2>
            {novelty.competitors.length === 0 ? (
              <p className="mt-2 text-sm text-muted-foreground">
                No overlapping repositories were found in the analyzed set.
              </p>
            ) : (
              <ul className="mt-3 flex flex-col gap-2">
                {novelty.competitors.map((competitor) => (
                  <li key={competitor.fullName} className="rounded-md border p-3 text-sm">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">{competitor.fullName}</span>
                      {competitor.primaryLanguage && <Badge variant="outline">{competitor.primaryLanguage}</Badge>}
                      <span className="ml-auto text-muted-foreground">
                        {competitor.stars.toLocaleString()}★ · overlap {Math.round(competitor.overlap * 100)}%
                      </span>
                    </div>
                    {competitor.reasons.length > 0 && (
                      <p className="mt-1 text-xs text-muted-foreground">{competitor.reasons.join(' · ')}</p>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="mt-4 rounded-lg border p-4">
            <h2 className="flex items-center gap-1.5 text-sm font-medium">
              <Lightbulb data-slot="icon" className="size-4" />
              Observed gaps
            </h2>
            <ul className="mt-2 flex flex-col gap-1.5">
              {result.gaps?.map((gap) => (
                <li key={gap.statement} className="text-sm">
                  <Badge variant={gap.kind === 'saturated' ? 'secondary' : 'outline'} className="mr-2">
                    {gap.kind}
                  </Badge>
                  <span className="text-muted-foreground">{gap.statement}</span>
                </li>
              ))}
            </ul>
            <p className="mt-2 text-xs text-muted-foreground">
              Gap statements are hypotheses scoped to the analyzed candidate set, not claims about
              GitHub as a whole.
            </p>
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

          {result.queryPlan && result.queryPlan.length > 0 && (
            <section className="mt-4 rounded-lg border p-4">
              <h2 className="text-sm font-medium">Search plan</h2>
              <ul className="mt-2 flex flex-col gap-1 text-sm text-muted-foreground">
                {result.queryPlan.map((query) => (
                  <li key={query.text}>
                    <Badge variant="ghost" className="mr-1 text-xs">
                      {query.source}
                    </Badge>
                    {query.text}
                  </li>
                ))}
              </ul>
            </section>
          )}

          <section className="mt-4 rounded-lg border border-dashed p-4">
            <h2 className="text-sm font-medium text-muted-foreground">Limitations</h2>
            <ul className="mt-2 flex list-disc flex-col gap-1 pl-4 text-xs text-muted-foreground">
              {result.limitations.map((limitation) => (
                <li key={limitation}>{limitation}</li>
              ))}
            </ul>
          </section>
        </div>
      )}

      {!result && !mutation.isPending && !mutation.isError && (
        <div className="mt-4 rounded-lg border p-8 text-center text-sm text-muted-foreground">
          Describe an idea to see how saturated or original its GitHub landscape looks.
        </div>
      )}
    </div>
  )
}

function noveltyBandLabel(score: number): string {
  if (score >= 70) return 'Mostly open space'
  if (score >= 45) return 'Partially explored'
  return 'Crowded'
}

function noveltyBand(score: number): 'outline' | 'secondary' | 'destructive' {
  if (score >= 70) return 'outline'
  if (score >= 45) return 'secondary'
  return 'destructive'
}
