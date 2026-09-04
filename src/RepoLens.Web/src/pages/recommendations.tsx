import { useEffect, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import {
  ChevronDown,
  Sparkles,
  Compass,
  Lightbulb,
  Network,
  ArrowRight,
  Target,
  ShieldCheck,
} from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { EvidenceBadge } from '@/components/evidence-badge'
import { ApiError, getJson } from '@/lib/api'
import { z } from 'zod'

const scoringWeightsSchema = z.object({
  originality: z.number().default(30),
  portfolioMarginal: z.number().default(20),
  feasibility: z.number().default(20),
  goalAlignment: z.number().default(15),
  interestAlignment: z.number().default(15),
})

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
  weights: scoringWeightsSchema.optional().default({
    originality: 30,
    portfolioMarginal: 20,
    feasibility: 20,
    goalAlignment: 15,
    interestAlignment: 15,
  }),
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

function fetchRecommendationById(
  replayId: string,
  signal?: AbortSignal,
): Promise<RecommendationResponse> {
  return getJson(`/api/recommendations/${replayId}`, recommendationResponseSchema, signal)
}

export function RecommendationPage() {
  const [searchParams] = useSearchParams()
  const replayId = searchParams.get('id')
  const goalParam = searchParams.get('goal')
  const usernameParam = searchParams.get('username')
  const interestsParam = searchParams.get('interests') ?? searchParams.get('category')

  const [goal, setGoal] = useState(
    goalParam ?? 'Land a .NET backend role with database depth',
  )
  const [interests, setInterests] = useState(
    interestsParam ?? 'databases, distributed systems',
  )
  const [username, setUsername] = useState(usernameParam ?? '')
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
    onSuccess: (data) => {
      setResult(data)
      if (data.items.length > 0) {
        setOpenItem(data.items[0].title)
      }
    },
  })

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!goal.trim()) return
    mutation.mutate()
  }

  useEffect(() => {
    if (replayId) {
      fetchRecommendationById(replayId)
        .then((data) => {
          setResult(data)
          setGoal(data.goal)
          setInterests(data.interests.join(', '))
          setUsername(data.username ?? '')
          setDuration(data.durationWeeks ?? '')
          if (data.items.length > 0) {
            setOpenItem(data.items[0].title)
          }
        })
        .catch(() => undefined)
    } else if (goalParam || usernameParam) {
      if (goalParam) setGoal(goalParam)
      if (usernameParam) setUsername(usernameParam)
      if (interestsParam) setInterests(interestsParam)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [replayId, goalParam, usernameParam, interestsParam])

  const weights = result?.weights ?? {
    originality: 30,
    portfolioMarginal: 20,
    feasibility: 20,
    goalAlignment: 15,
    interestAlignment: 15,
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Decide What to Build Next"
        subtitle="Ranked against live GitHub search results & pgvector clusters using deterministic scoring. No speculative LLM hallucination."
      />

      <form onSubmit={submit} className="space-y-4 rounded-xl border bg-card p-6 shadow-sm">
        <div className="space-y-1">
          <label htmlFor="goal-input" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            Your Primary Goal
          </label>
          <input
            id="goal-input"
            value={goal}
            onChange={(e) => setGoal(e.target.value)}
            placeholder="e.g. Build a portfolio piece showing low-level database engine depth"
            aria-label="Your goal"
            className="w-full rounded-lg border bg-background px-3 py-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-primary"
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <label className="space-y-1 sm:col-span-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Topics & Interests (comma-separated)
            </span>
            <input
              value={interests}
              onChange={(e) => setInterests(e.target.value)}
              placeholder="e.g. databases, distributed systems, wal"
              aria-label="Topics and interests"
              className="w-full rounded-lg border bg-background px-3 py-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-primary"
            />
          </label>

          <label className="space-y-1">
            <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              GitHub Username (Optional)
            </span>
            <input
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="e.g. torvalds"
              aria-label="GitHub username"
              className="w-full rounded-lg border bg-background px-3 py-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-primary"
            />
          </label>
        </div>

        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-xs text-muted-foreground">
            <span>Target timeframe:</span>
            <select
              value={duration}
              onChange={(e) => setDuration(e.target.value)}
              aria-label="Target timeframe"
              className="rounded border bg-background px-2 py-1 text-xs text-foreground outline-none"
            >
              {[2, 4, 6, 8, 12].map((weeks) => (
                <option key={weeks} value={String(weeks)}>
                  {weeks} weeks
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="flex items-center justify-between pt-1">
          <span className="text-xs text-muted-foreground">
            Ranked with deterministic formula rec-v1 (Originality {weights.originality}% &bull; Portfolio {weights.portfolioMarginal}% &bull; Feasibility {weights.feasibility}% &bull; Goal {weights.goalAlignment}% &bull; Interest {weights.interestAlignment}%)
          </span>
          <Button type="submit" disabled={mutation.isPending} className="gap-2 shrink-0">
            <Compass className="h-4 w-4" />
            {mutation.isPending ? 'Surveying GitHub Landscape…' : 'Generate 3 Ranked Projects'}
          </Button>
        </div>
      </form>

      {mutation.isPending && (
        <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed p-12 text-center text-sm text-muted-foreground">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          <p className="font-medium text-foreground">Brainstorming candidate ideas & checking GitHub competition…</p>
          <p className="max-w-md text-xs">
            Evaluating candidate pools in pgvector, scoring marginal portfolio value, and computing deterministic rec-v1 scores.
          </p>
        </div>
      )}

      {mutation.isError && (
        <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-semibold text-destructive">Recommendation failed</p>
          <p className="mt-1 text-xs text-muted-foreground">
            {mutation.error instanceof ApiError ? mutation.error.message : 'Something went wrong.'}
          </p>
        </div>
      )}

      {result && (
        <div className="space-y-6">
          {/* Metadata Bar */}
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/20 px-4 py-2 text-xs text-muted-foreground">
            <div className="flex items-center gap-2">
              <EvidenceBadge
                type="deterministic"
                label={`rec-v${result.version}`}
                subtext={`Multi-factor deterministic ranking: Originality ${weights.originality}%, Portfolio Marginal ${weights.portfolioMarginal}%, Feasibility ${weights.feasibility}%, Goal ${weights.goalAlignment}%, Interest ${weights.interestAlignment}%`}
              />
              {result.servedFromCache && (
                <Badge variant="secondary" className="text-[11px]">
                  Cached analysis
                </Badge>
              )}
            </div>
            <span>Run #{result.id} &bull; 3 distinct proposals</span>
          </div>

          {/* Recommended Items */}
          <ol className="space-y-4">
            {result.items.map((item, index) => {
              const open = openItem === item.title
              const scorePct = Math.round(item.score * 100)

              return (
                <li key={item.title} className="rounded-xl border bg-card shadow-sm overflow-hidden">
                  <button
                    type="button"
                    onClick={() => setOpenItem(open ? null : item.title)}
                    className="flex w-full items-start gap-4 p-5 text-left hover:bg-muted/20 transition-colors"
                  >
                    <span className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-sm">
                      {index + 1}
                    </span>

                    <div className="min-w-0 flex-1 space-y-1.5">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-bold text-base text-foreground">{item.title}</span>
                        <Badge variant="outline" className="text-xs">
                          {item.category}
                        </Badge>
                        <span className="ml-auto rounded-full bg-primary/10 px-2.5 py-0.5 font-mono text-xs font-bold text-primary">
                          {scorePct}/100 Match
                        </span>
                      </div>

                      <p className="text-sm text-muted-foreground leading-relaxed">{item.summary}</p>

                      <div className="flex flex-wrap gap-2 pt-1 text-xs text-muted-foreground">
                        {item.whyRankedHere.map((line) => (
                          <span key={line} className="rounded bg-muted/50 px-2 py-0.5 text-[11px]">
                            &bull; {line}
                          </span>
                        ))}
                      </div>
                    </div>

                    <ChevronDown
                      className={`mt-1 h-5 w-5 shrink-0 text-muted-foreground transition-transform ${open ? 'rotate-180' : ''}`}
                    />
                  </button>

                  {open && (
                    <div className="border-t bg-muted/10 px-6 py-5 space-y-6">
                      {/* Problem & Target User */}
                      <div className="grid gap-4 sm:grid-cols-2">
                        <div className="rounded-lg border bg-background p-3.5 space-y-1">
                          <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                            Problem Solved
                          </h4>
                          <p className="text-xs text-foreground leading-relaxed">{item.problem}</p>
                        </div>
                        <div className="rounded-lg border bg-background p-3.5 space-y-1">
                          <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                            Target Audience & Users
                          </h4>
                          <p className="text-xs text-foreground leading-relaxed">{item.targetUser}</p>
                        </div>
                      </div>

                      {/* Explicit Score Breakdown Weights */}
                      <div className="space-y-2">
                        <div className="flex items-center justify-between">
                          <h4 className="text-xs font-bold uppercase tracking-wider text-foreground flex items-center gap-1.5">
                            <ShieldCheck className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                            <span>Deterministic Evidence Scoring Breakdown (rec-v1)</span>
                          </h4>
                          <span className="text-xs text-muted-foreground font-mono">
                            Rank Score: {scorePct}/100
                          </span>
                        </div>

                        <div className="grid gap-2 sm:grid-cols-3 lg:grid-cols-5 text-xs">
                          <div className="rounded-lg border bg-background p-2.5 space-y-1">
                            <div className="flex justify-between text-[11px]">
                              <span className="text-muted-foreground">Originality</span>
                              <span className="font-semibold">{weights.originality}% wt</span>
                            </div>
                            <p className="font-mono text-base font-bold text-foreground">
                              {Math.round(item.evidence.originality * 100)}%
                            </p>
                          </div>

                          <div className="rounded-lg border bg-background p-2.5 space-y-1">
                            <div className="flex justify-between text-[11px]">
                              <span className="text-muted-foreground">Portfolio Value</span>
                              <span className="font-semibold">{weights.portfolioMarginal}% wt</span>
                            </div>
                            <p className="font-mono text-base font-bold text-foreground">
                              {Math.round(item.evidence.portfolioMarginalValue * 100)}%
                            </p>
                          </div>

                          <div className="rounded-lg border bg-background p-2.5 space-y-1">
                            <div className="flex justify-between text-[11px]">
                              <span className="text-muted-foreground">Feasibility</span>
                              <span className="font-semibold">{weights.feasibility}% wt</span>
                            </div>
                            <p className="font-mono text-base font-bold text-foreground">
                              {Math.round(item.evidence.feasibility.score * 100)}%
                            </p>
                          </div>

                          <div className="rounded-lg border bg-background p-2.5 space-y-1">
                            <div className="flex justify-between text-[11px]">
                              <span className="text-muted-foreground">Goal Alignment</span>
                              <span className="font-semibold">{weights.goalAlignment}% wt</span>
                            </div>
                            <p className="font-mono text-base font-bold text-foreground">
                              {Math.round(item.evidence.goalAlignment * 100)}%
                            </p>
                          </div>

                          <div className="rounded-lg border bg-background p-2.5 space-y-1">
                            <div className="flex justify-between text-[11px]">
                              <span className="text-muted-foreground">Interest Match</span>
                              <span className="font-semibold">{weights.interestAlignment}% wt</span>
                            </div>
                            <p className="font-mono text-base font-bold text-foreground">
                              {Math.round(item.evidence.interestAlignment * 100)}%
                            </p>
                          </div>
                        </div>

                        <p className="text-[11px] text-muted-foreground">
                          Candidate pool: Checked against {item.evidence.landscape.candidateCount} candidate repositories discovered on GitHub.
                          {item.evidence.landscape.competitors.length > 0 && (
                            <span> Closest existing repos: {item.evidence.landscape.competitors.join(', ')}.</span>
                          )}
                        </p>
                      </div>

                      {/* Differentiation & Suggested MVP */}
                      <div className="grid gap-4 sm:grid-cols-2 text-xs">
                        <div className="rounded-lg border bg-background p-3.5 space-y-1">
                          <h4 className="font-semibold text-foreground">Differentiation Opportunity</h4>
                          <p className="text-muted-foreground leading-relaxed">
                            {item.differentiationOpportunity}
                          </p>
                        </div>

                        <div className="rounded-lg border bg-background p-3.5 space-y-1">
                          <h4 className="font-semibold text-foreground">Suggested MVP Architecture</h4>
                          <p className="text-muted-foreground leading-relaxed">
                            {item.mvpSuggestion}
                          </p>
                        </div>
                      </div>

                      {/* Action Bridges for this specific recommendation */}
                      <div className="pt-2 border-t flex flex-wrap items-center gap-3">
                        <Link
                          to={`/validate?idea=${encodeURIComponent(`${item.title}: ${item.summary}`)}`}
                          className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-3.5 py-1.5 text-xs font-semibold text-primary-foreground hover:bg-primary/90 transition-colors shadow-2xs"
                        >
                          <Lightbulb className="h-3.5 w-3.5" />
                          <span>Validate this Idea on GitHub</span>
                          <ArrowRight className="h-3.5 w-3.5" />
                        </Link>

                        <Link
                          to={`/ecosystem?query=${encodeURIComponent(item.category || item.title)}`}
                          className="inline-flex items-center gap-1.5 rounded-lg border bg-background px-3 py-1.5 text-xs font-medium text-foreground hover:bg-muted/50 transition-colors"
                        >
                          <Network className="h-3.5 w-3.5" />
                          <span>Explore Domain Landscape</span>
                          <ArrowRight className="h-3.5 w-3.5" />
                        </Link>
                      </div>
                    </div>
                  )}
                </li>
              )
            })}
          </ol>

          {/* Action Bridge Back to Portfolio */}
          {username && (
            <section className="rounded-xl border bg-muted/20 p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
              <div className="flex items-center gap-2">
                <Target className="h-4 w-4 text-primary" />
                <span className="text-muted-foreground">
                  Looking to assess other capability areas for @{username}?
                </span>
              </div>
              <Link
                to={`/portfolio?username=${encodeURIComponent(username)}`}
                className="font-semibold text-primary hover:underline inline-flex items-center gap-1"
              >
                <span>Return to Portfolio Analysis</span>
                <ArrowRight className="h-3 w-3" />
              </Link>
            </section>
          )}

          <p className="text-xs text-muted-foreground text-center">{result.limitations}</p>
        </div>
      )}

      {!result && !mutation.isPending && !mutation.isError && (
        <div className="rounded-xl border border-dashed p-10 text-center text-xs text-muted-foreground space-y-2">
          <Sparkles className="mx-auto h-8 w-8 opacity-40" />
          <p className="font-medium text-foreground text-sm">Define your goal to get ranked project recommendations</p>
          <p className="max-w-md mx-auto">
            RepoLens generates distinct concepts, checks existing GitHub competition in pgvector space, and scores marginal value for your portfolio.
          </p>
        </div>
      )}
    </div>
  )
}
