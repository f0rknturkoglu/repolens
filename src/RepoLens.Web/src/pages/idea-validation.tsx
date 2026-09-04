import { useEffect, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import {
  Lightbulb,
  Radar,
  ExternalLink,
  BookOpen,
  Network,
  Sparkles,
  ArrowRight,
  Search,
} from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/layout'
import { EvidenceBadge } from '@/components/evidence-badge'
import { ScoreExplainer, type ScoreComponent } from '@/components/score-explainer'
import { ApiError, getJson } from '@/lib/api'
import { z } from 'zod'

const noveltySchema = z.object({
  score: z.number(),
  formulaVersion: z.string(),
  components: z.array(
    z.object({
      key: z.string(),
      label: z.string(),
      value: z.number(),
      evidence: z.string(),
      weightPercent: z.number().optional(),
    }),
  ),
  competitors: z.array(
    z.object({
      repositoryId: z.number().nullable().optional(),
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
  clusters: z
    .array(
      z.object({
        label: z.string(),
        memberCount: z.number(),
        topRepositories: z.array(z.string()),
      }),
    )
    .nullable(),
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

const SAMPLE_IDEA =
  'A PostgreSQL migration load-testing laboratory that replays real schema-change workloads'

function fetchIdeaValidation(
  replayId: string,
  signal?: AbortSignal,
): Promise<IdeaValidationResponse> {
  return getJson(`/api/analysis/idea/${replayId}`, ideaValidationResponseSchema, signal)
}

function getComponentWeight(key: string): number {
  switch (key) {
    case 'competitor_gap':
      return 30
    case 'density':
      return 25
    case 'cluster_dominance':
      return 20
    case 'active_competition':
      return 15
    case 'concept_redundancy':
      return 10
    default:
      return 20
  }
}

function noveltyBandLabel(score: number): string {
  if (score >= 70) return 'Mostly open space'
  if (score >= 45) return 'Partially explored'
  return 'Crowded space'
}

export function IdeaValidationPage() {
  const [searchParams] = useSearchParams()
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

  useEffect(() => {
    const replayId = searchParams.get('id')
    const ideaParam = searchParams.get('idea')

    if (replayId) {
      fetchIdeaValidation(replayId).then(setResult).catch(() => undefined)
    } else if (ideaParam) {
      setIdea(ideaParam)
      mutation.mutate(ideaParam)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Map components for ScoreExplainer (backend weightPercent is single source of truth)
  const scoreComponents: ScoreComponent[] =
    novelty?.components.map((c) => ({
      name: c.label,
      weightPercent: c.weightPercent ?? getComponentWeight(c.key),
      score: c.value,
      description: c.evidence,
    })) ?? []

  // Generate suggested query for ecosystem jump
  const ecosystemKeywords = idea
    .replace(/[^\w\s]/g, '')
    .split(/\s+/)
    .filter((w) => w.length > 3)
    .slice(0, 3)
    .join(' ')

  return (
    <div className="space-y-6">
      <PageHeader
        title="Validate an Idea"
        subtitle="Systematically survey GitHub competition, measure idea originality with deterministic formulas, and detect unserved niches."
      />

      <form onSubmit={submit} className="flex flex-col gap-3 rounded-xl border bg-card p-4 shadow-sm">
        <label htmlFor="idea-input" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          Project Concept or Specification
        </label>
        <textarea
          id="idea-input"
          value={idea}
          onChange={(e) => setIdea(e.target.value)}
          rows={3}
          placeholder="Describe your project idea in detail…"
          aria-label="Project idea"
          className="w-full resize-y rounded-lg border bg-background px-3 py-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-primary"
        />
        <div className="flex items-center justify-between">
          <span className="text-xs text-muted-foreground">
            RepoLens formulates a multi-query search plan & scores candidate vectors.
          </span>
          <Button type="submit" disabled={mutation.isPending} className="gap-2">
            <Radar className="h-4 w-4" />
            {mutation.isPending ? 'Analyzing Candidates…' : 'Validate Idea'}
          </Button>
        </div>
      </form>

      {mutation.isPending && (
        <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed p-12 text-center text-sm text-muted-foreground">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          <p className="font-medium text-foreground">Executing multi-angle GitHub search plan…</p>
          <p className="max-w-md text-xs">
            Gathering candidate repositories, computing pgvector cosine embeddings, and calculating deterministic novelty-v1 metrics.
          </p>
        </div>
      )}

      {mutation.isError && (
        <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6" role="alert">
          <p className="text-sm font-semibold text-destructive">Validation failed</p>
          <p className="mt-1 text-xs text-muted-foreground">
            {mutation.error instanceof ApiError ? mutation.error.message : 'Something went wrong.'}
          </p>
        </div>
      )}

      {result && result.status === 'failed' && (
        <div className="rounded-xl border p-6">
          <p className="text-sm font-medium">Validation could not complete</p>
          <p className="mt-1 text-xs text-muted-foreground">
            GitHub could not be reached or returned no candidate repositories for this query plan.
          </p>
        </div>
      )}

      {result && result.status === 'completed' && novelty && (
        <div className="space-y-6">
          {/* Metadata bar */}
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/20 px-4 py-2 text-xs text-muted-foreground">
            <div className="flex items-center gap-2">
              <EvidenceBadge
                type="deterministic"
                label={novelty.formulaVersion}
                version="v1"
                subtext={`Deterministic formula weights: ${scoreComponents.map((c) => `${c.weightPercent}% ${c.name.toLowerCase()}`).join(', ')}`}
              />
              {result.servedFromCache && (
                <Badge variant="secondary" className="text-[11px]">
                  Cached analysis
                </Badge>
              )}
            </div>
            <span>Analysis run #{result.id} &bull; {new Date(result.createdAtUtc).toLocaleDateString()}</span>
          </div>

          {/* Primary Score Explainer Component */}
          <ScoreExplainer
            title="Estimated Idea Novelty"
            description="Measures how uncrowded this concept is relative to discovered GitHub repositories."
            score={novelty.score}
            formula="novelty-v1"
            band={noveltyBandLabel(novelty.score)}
            candidateCount={novelty.competitors.length ? undefined : undefined}
            components={scoreComponents}
          />

          {/* Competitors Section */}
          <section className="rounded-xl border bg-card p-5 shadow-sm space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-foreground">Closest Discovered Competitors</h3>
                <p className="text-xs text-muted-foreground">
                  The repositories most semantically and taxonomically overlapping with your concept.
                </p>
              </div>
              <EvidenceBadge
                type="candidate-scoped"
                label="Candidate Set"
                subtext="Derived from GitHub search plan results"
              />
            </div>

            {novelty.competitors.length === 0 ? (
              <p className="text-xs text-muted-foreground py-4 text-center">
                No overlapping repositories were found in the analyzed candidate set.
              </p>
            ) : (
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {novelty.competitors.map((competitor) => {
                  const overlapPct = Math.round(competitor.overlap * 100)
                  return (
                    <div
                      key={competitor.fullName}
                      className="flex flex-col justify-between rounded-lg border bg-background p-4 shadow-2xs space-y-3"
                    >
                      <div>
                        <div className="flex items-start justify-between gap-2">
                          <span className="font-semibold text-sm text-foreground break-all">
                            {competitor.fullName}
                          </span>
                          <a
                            href={`https://github.com/${competitor.fullName}`}
                            target="_blank"
                            rel="noreferrer"
                            className="text-muted-foreground hover:text-foreground transition-colors p-0.5 shrink-0"
                            title="Open on GitHub"
                          >
                            <ExternalLink className="h-4 w-4" />
                          </a>
                        </div>

                        <div className="mt-2 flex flex-wrap items-center gap-2 text-xs">
                          {competitor.primaryLanguage && (
                            <Badge variant="outline" className="text-[11px]">
                              {competitor.primaryLanguage}
                            </Badge>
                          )}
                          <span className="text-muted-foreground">
                            {competitor.stars.toLocaleString()}★
                          </span>
                          {competitor.isArchived && (
                            <Badge variant="destructive" className="text-[10px]">
                              Archived
                            </Badge>
                          )}
                        </div>

                        {/* Overlap bar */}
                        <div className="mt-3 space-y-1">
                          <div className="flex items-center justify-between text-[11px]">
                            <span className="text-muted-foreground">Overlap</span>
                            <span className="font-semibold text-foreground">{overlapPct}%</span>
                          </div>
                          <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
                            <div
                              className="h-full bg-primary/70 transition-all"
                              style={{ width: `${Math.min(100, Math.max(0, overlapPct))}%` }}
                            />
                          </div>
                        </div>

                        {competitor.reasons.length > 0 && (
                          <div className="mt-3 flex flex-wrap gap-1">
                            {competitor.reasons.map((reason) => (
                              <span
                                key={reason}
                                className="rounded bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground"
                              >
                                {reason}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>

                      <div className="pt-2 border-t flex items-center justify-between text-xs">
                        {competitor.repositoryId ? (
                          <Link
                            to={`/repositories/${competitor.repositoryId}`}
                            className="inline-flex items-center gap-1 text-primary hover:underline font-medium"
                          >
                            <BookOpen className="h-3.5 w-3.5" />
                            <span>Inspect Details</span>
                          </Link>
                        ) : (
                          <Link
                            to={`/search?q=${encodeURIComponent(competitor.fullName)}`}
                            className="inline-flex items-center gap-1 text-muted-foreground hover:text-foreground"
                          >
                            <Search className="h-3.5 w-3.5" />
                            <span>Search Index</span>
                          </Link>
                        )}
                      </div>
                    </div>
                  )
                })}
              </div>
            )}
          </section>

          {/* Observed Gaps & Market Opportunities */}
          <section className="rounded-xl border bg-card p-5 shadow-sm space-y-3">
            <div className="flex items-center gap-2">
              <Lightbulb className="h-5 w-5 text-amber-500" />
              <h3 className="text-base font-bold text-foreground">Observed Gaps & Opportunities</h3>
            </div>
            <p className="text-xs text-muted-foreground">
              Niche hypotheses identified by comparing candidate project capabilities against common developer requirements.
            </p>

            <ul className="space-y-2 pt-1">
              {result.gaps && result.gaps.length > 0 ? (
                result.gaps.map((gap) => (
                  <li
                    key={gap.statement}
                    className="flex items-start gap-2.5 rounded-lg border bg-muted/30 p-3 text-xs"
                  >
                    <Badge
                      variant={gap.kind === 'saturated' ? 'secondary' : 'outline'}
                      className="shrink-0 uppercase text-[10px] tracking-wider"
                    >
                      {gap.kind}
                    </Badge>
                    <span className="text-foreground leading-relaxed">{gap.statement}</span>
                  </li>
                ))
              ) : (
                <li className="text-xs text-muted-foreground">
                  No structural gaps were detected in this specific candidate set.
                </li>
              )}
            </ul>
          </section>

          {/* Search Plan Transparency */}
          {result.queryPlan && result.queryPlan.length > 0 && (
            <section className="rounded-xl border bg-card p-5 shadow-sm space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-sm font-bold text-foreground">Multi-Angle Search Plan</h3>
                  <p className="text-xs text-muted-foreground">
                    Queries executed across GitHub APIs to collect candidate projects from diverse taxonomy angles.
                  </p>
                </div>
                <EvidenceBadge
                  type="llm-assisted"
                  label="Query Expansion"
                  subtext="LLMs suggest search queries; results and embeddings are deterministic"
                />
              </div>

              <div className="grid gap-2 sm:grid-cols-2">
                {result.queryPlan.map((query) => (
                  <div
                    key={query.text}
                    className="flex items-center justify-between rounded-md border bg-muted/20 px-3 py-2 text-xs"
                  >
                    <span className="font-mono text-[11px] text-foreground truncate mr-2">
                      {query.text}
                    </span>
                    <Badge variant="ghost" className="text-[10px] shrink-0">
                      {query.source}
                    </Badge>
                  </div>
                ))}
              </div>
            </section>
          )}

          {/* Action Bridges (Primary Decision Journey) */}
          <section className="rounded-xl border border-primary/20 bg-primary/5 p-6 space-y-4">
            <div className="flex items-center gap-2">
              <Sparkles className="h-5 w-5 text-primary" />
              <h3 className="font-bold text-base text-foreground">Next Steps in Decision Journey</h3>
            </div>
            <p className="text-xs text-muted-foreground leading-relaxed">
              Now that you&rsquo;ve validated this idea&rsquo;s competitive posture, explore the surrounding ecosystem clusters or find high-marginal-value projects tailored to your goals.
            </p>

            <div className="flex flex-wrap gap-3 pt-2">
              <Link
                to={`/ecosystem?query=${encodeURIComponent(ecosystemKeywords || idea)}`}
                className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-xs hover:bg-primary/90 transition-colors"
              >
                <Network className="h-4 w-4" />
                <span>Explore Full Ecosystem Landscape</span>
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>

              <Link
                to={`/recommend?goal=${encodeURIComponent(idea)}`}
                className="inline-flex items-center gap-2 rounded-lg border bg-background px-4 py-2 text-xs font-medium text-foreground hover:bg-muted/50 transition-colors"
              >
                <Lightbulb className="h-4 w-4" />
                <span>Find What to Build Around This Gap</span>
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </div>
          </section>

          {/* Methodology Limitations Card */}
          <section className="rounded-xl border border-dashed p-4 text-xs text-muted-foreground space-y-2">
            <h4 className="font-semibold text-foreground">Candidate-Set Boundary & Limitations</h4>
            <ul className="list-disc pl-4 space-y-1">
              {result.limitations.map((limitation) => (
                <li key={limitation}>{limitation}</li>
              ))}
            </ul>
          </section>
        </div>
      )}

      {!result && !mutation.isPending && !mutation.isError && (
        <div className="rounded-xl border border-dashed p-10 text-center text-xs text-muted-foreground space-y-2">
          <Lightbulb className="mx-auto h-8 w-8 opacity-40" />
          <p className="font-medium text-foreground text-sm">Enter an idea to start validation</p>
          <p className="max-w-md mx-auto">
            RepoLens will generate a search plan, query GitHub, analyze candidates in pgvector space, and compute an evidence-backed novelty score.
          </p>
        </div>
      )}
    </div>
  )
}
