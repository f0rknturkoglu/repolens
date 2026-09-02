import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { ChevronDown, Compass } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { ApiError, getJson } from '@/lib/api'
import { z } from 'zod'

const recommendationResponseSchema = z.object({
  id: z.number(),
  version: z.string(),
  status: z.string(),
  goal: z.string(),
  username: z.string().nullable(),
  interests: z.array(z.string()),
  durationWeeks: z.string().nullable(),
  servedFromCache: z.boolean(),
  createdAtUtc: z.string(),
  items: z.array(
    z.object({
      title: z.string(),
      category: z.string(),
      summary: z.string(),
      problem: z.string(),
      targetUser: z.string(),
      score: z.number(),
      whyRankedHere: z.array(z.string()),
      differentiationOpportunity: z.string(),
      mvpSuggestion: z.string(),
      evidence: z.object({
        originality: z.number(),
        portfolioMarginalValue: z.number(),
        goalAlignment: z.number(),
        interestAlignment: z.number(),
        feasibility: z.object({
          scope: z.string(),
          score: z.number(),
          reasons: z.array(z.string()),
        }),
        landscape: z.object({
          candidateCount: z.number(),
          noveltyScore: z.number(),
          density: z.number(),
          largestClusterShare: z.number(),
          activeCount: z.number(),
          competitors: z.array(z.string()),
        }),
      }),
    }),
  ),
  limitations: z.string(),
})

type RecommendationResponse = z.infer<typeof recommendationResponseSchema>

function requestRecommendations(
  payload: { goal: string; interests: string[]; username?: string; durationWeeks?: string },
  signal?: AbortSignal,
): Promise<RecommendationResponse> {
  return getJson('/api/recommendations', recommendationResponseSchema, signal, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(payload),
  })
}

export function RecommendationPage() {
  const [goal, setGoal] = useState('Land a .NET backend role with database depth')
  const [interests, setInterests] = useState('databases, distributed systems')
  const [username, setUsername] = useState('')
  const [duration, setDuration] = useState('6')
  const [result, setResult] = useState<RecommendationResponse | null>(null)
  const [openItem, setOpenItem] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: () =>
      requestRecommendations({
        goal,
        interests: interests.split(',').map((i) => i.trim()).filter(Boolean),
        username: username.trim() || undefined,
        durationWeeks: duration || undefined,
      }),
    onSuccess: (data) => setResult(data),
  })

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!goal.trim()) return
    mutation.mutate()
  }

  return (
    <div>
      <PageHeader
        title="Find My Next Project"
        subtitle="A goal, your interests, an optional GitHub profile — RepoLens proposes 3 distinct, evidence-backed projects it has checked against the GitHub landscape."
      />

      <form onSubmit={submit} className="flex flex-col gap-3 rounded-lg border p-4">
        <label className="flex flex-col gap-1.5">
          <span className="text-sm text-muted-foreground">Goal</span>
          <textarea
            value={goal}
            onChange={(e) => setGoal(e.target.value)}
            rows={2}
            aria-label="Goal"
            className="w-full resize-y rounded-lg border bg-background px-3 py-2 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          />
        </label>
        <div className="grid gap-3 sm:grid-cols-3">
          <label className="flex flex-col gap-1.5">
            <span className="text-sm text-muted-foreground">Interests (comma separated)</span>
            <input
              value={interests}
              onChange={(e) => setInterests(e.target.value)}
              aria-label="Interests"
              className="h-9 rounded-lg border bg-background px-3 text-sm outline-none"
            />
          </label>
          <label className="flex flex-col gap-1.5">
            <span className="text-sm text-muted-foreground">GitHub profile (optional)</span>
            <input
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="octocat"
              aria-label="GitHub username"
              className="h-9 rounded-lg border bg-background px-3 text-sm outline-none"
            />
          </label>
          <label className="flex flex-col gap-1.5">
            <span className="text-sm text-muted-foreground">Time budget (weeks)</span>
            <select
              value={duration}
              onChange={(e) => setDuration(e.target.value)}
              aria-label="Duration weeks"
              className="h-9 rounded-lg border bg-background px-3 text-sm outline-none"
            >
              <option value="">Flexible</option>
              {[2, 4, 6, 8, 12].map((weeks) => (
                <option key={weeks} value={String(weeks)}>
                  {weeks} weeks
                </option>
              ))}
            </select>
          </label>
        </div>
        <div>
          <Button type="submit" disabled={mutation.isPending} className="gap-1.5">
            <Compass data-slot="icon" />
            {mutation.isPending ? 'Analyzing…' : 'Find my next project'}
          </Button>
        </div>
      </form>

      {mutation.isPending && (
        <div className="mt-4 rounded-lg border p-8 text-center text-sm text-muted-foreground">
          Brainstorming candidates and validating each against GitHub…
        </div>
      )}

      {mutation.isError && (
        <div className="mt-4 rounded-lg border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-medium text-destructive">Recommendation failed</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {mutation.error instanceof ApiError ? mutation.error.message : 'Something went wrong.'}
          </p>
        </div>
      )}

      {result && (
        <div className="mt-4">
          <div className="mb-3 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            {result.servedFromCache && <Badge variant="secondary">cached (identical request recently analyzed)</Badge>}
            <Badge variant="outline">rec {result.version}</Badge>
            <span>analysis #{result.id}</span>
          </div>

          <ol className="flex flex-col gap-3">
            {result.items.map((item, index) => {
              const open = openItem === item.title
              return (
                <li key={item.title} className="rounded-lg border bg-background">
                  <button
                    type="button"
                    onClick={() => setOpenItem(open ? null : item.title)}
                    className="flex w-full items-start gap-3 p-4 text-left"
                  >
                    <span className="mt-0.5 flex size-6 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold">
                      {index + 1}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="font-medium">{item.title}</span>
                        <Badge variant="outline">{item.category}</Badge>
                      </span>
                      <span className="mt-0.5 block text-sm text-muted-foreground">{item.summary}</span>
                      <span className="mt-2 block space-y-0.5 text-xs text-muted-foreground">
                        {item.whyRankedHere.map((line) => (
                          <span key={line} className="block">{line}</span>
                        ))}
                      </span>
                    </span>
                    <ChevronDown
                      data-slot="icon"
                      className={`mt-1 size-4 shrink-0 text-muted-foreground transition-transform ${open ? 'rotate-180' : ''}`}
                    />
                  </button>
                  {open && (
                    <div className="border-t px-4 py-4 pl-13 text-sm">
                      <div className="grid gap-3 sm:grid-cols-2">
                        <Detail label="Problem" value={item.problem} />
                        <Detail label="Target user" value={item.targetUser} />
                      </div>
                      <h4 className="mt-4 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        Evidence
                      </h4>
                      <ul className="mt-1.5 space-y-1 text-muted-foreground">
                        <li>Estimated novelty: {Math.round(item.evidence.landscape.noveltyScore)}/100 over {item.evidence.landscape.candidateCount} candidates</li>
                        <li>Feasibility: {item.evidence.feasibility.scope} scope ({item.evidence.feasibility.reasons.join(' ')})</li>
                        <li>Portfolio marginal value: {Math.round(item.evidence.portfolioMarginalValue * 100)}%</li>
                        <li>Goal alignment {Math.round(item.evidence.goalAlignment * 100)}% · Interest alignment {Math.round(item.evidence.interestAlignment * 100)}%</li>
                        {item.evidence.landscape.competitors.length > 0 && (
                          <li>Closest competitors: {item.evidence.landscape.competitors.join(', ')}</li>
                        )}
                      </ul>
                      <h4 className="mt-4 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        Differentiation
                      </h4>
                      <p className="mt-1 text-muted-foreground">{item.differentiationOpportunity}</p>
                      <h4 className="mt-4 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        Suggested MVP
                      </h4>
                      <p className="mt-1 text-muted-foreground">{item.mvpSuggestion}</p>
                    </div>
                  )}
                </li>
              )
            })}
          </ol>

          <p className="mt-3 text-xs text-muted-foreground">{result.limitations}</p>
        </div>
      )}

      {!result && !mutation.isPending && !mutation.isError && (
        <div className="mt-4 rounded-lg border p-8 text-center text-sm text-muted-foreground">
          Tell RepoLens your goal and interests to get three validated project ideas.
        </div>
      )}
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <h4 className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</h4>
      <p className="mt-1 text-muted-foreground">{value}</p>
    </div>
  )
}
