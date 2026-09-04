import { Link } from 'react-router-dom'
import {
  ArrowRight,
  Lightbulb,
  Network,
  Sparkles,
  Compass,
  Search,
  UserCheck,
  ShieldCheck,
  Cpu,
  Database,
  Layers,
} from 'lucide-react'
import { ApiStatusBadge } from '@/components/api-status'
import { EvidenceBadge } from '@/components/evidence-badge'

const sampleIdeas = [
  'PostgreSQL zero-downtime schema migration runner',
  'Real-time WebSocket reverse proxy and traffic inspector in Rust',
  'Local-first markdown knowledge base with vector similarity search',
  'Git worktree and multi-agent coordination CLI',
]

export function HomePage() {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-12 py-6">
      {/* Hero Section */}
      <section className="flex flex-col items-start gap-5">
        <div className="flex flex-wrap items-center gap-2">
          <EvidenceBadge
            type="deterministic"
            label="novelty-v1 &bull; marginal-v1"
            subtext="Scored deterministically using pgvector cosine distance and connected graph components"
          />
          <span className="text-xs text-muted-foreground">•</span>
          <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            Evidence-Backed GitHub Intelligence
          </span>
        </div>

        <h1 className="text-3xl font-extrabold tracking-tight sm:text-5xl text-foreground max-w-3xl leading-tight">
          Know what&rsquo;s already built before you write the first line.
        </h1>

        <p className="text-base sm:text-lg text-muted-foreground max-w-2xl leading-relaxed">
          RepoLens systematically surveys GitHub competition, clusters existing technical ecosystems, and
          measures project originality with verifiable mathematical evidence &mdash; not LLM vibes.
        </p>

        {/* Action CTAs */}
        <div className="flex flex-wrap items-center gap-3 pt-2">
          <Link
            to="/validate"
            className="inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground shadow-sm hover:bg-primary/90 transition-colors"
          >
            <Lightbulb className="h-4 w-4" />
            <span>Validate an Idea</span>
            <ArrowRight className="h-4 w-4" />
          </Link>
          <Link
            to="/ecosystem"
            className="inline-flex items-center gap-2 rounded-lg border bg-background px-4 py-2.5 text-sm font-medium text-foreground hover:bg-muted/50 transition-colors"
          >
            <Network className="h-4 w-4" />
            <span>Explore Technical Ecosystems</span>
          </Link>
          <Link
            to="/recommend"
            className="inline-flex items-center gap-2 rounded-lg border bg-background px-4 py-2.5 text-sm font-medium text-foreground hover:bg-muted/50 transition-colors"
          >
            <Sparkles className="h-4 w-4" />
            <span>Find Next Project</span>
          </Link>
        </div>

        {/* Quick Click Idea Prompts */}
        <div className="w-full rounded-xl border bg-muted/30 p-4">
          <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-2.5">
            Try validating one of these project concepts:
          </p>
          <div className="flex flex-wrap gap-2">
            {sampleIdeas.map((idea) => (
              <Link
                key={idea}
                to={`/validate?idea=${encodeURIComponent(idea)}`}
                className="inline-flex items-center gap-1.5 rounded-md border bg-background px-3 py-1.5 text-xs font-medium text-foreground hover:border-primary hover:text-primary transition-all shadow-2xs"
              >
                <span>&ldquo;{idea}&rdquo;</span>
                <ArrowRight className="h-3 w-3 opacity-60" />
              </Link>
            ))}
          </div>
        </div>
      </section>

      {/* Two Core User Journeys */}
      <section className="space-y-4">
        <h2 className="text-xl font-bold tracking-tight text-foreground">
          Two Systematic Decision Journeys
        </h2>

        <div className="grid gap-6 md:grid-cols-2">
          {/* Journey 1: The Builder's Loop */}
          <div className="flex flex-col justify-between rounded-xl border bg-card p-6 shadow-sm">
            <div className="space-y-3">
              <div className="inline-flex items-center gap-2 rounded-md bg-emerald-500/10 px-2.5 py-1 text-xs font-semibold text-emerald-700 dark:text-emerald-400 border border-emerald-500/20">
                Primary Decision Journey
              </div>
              <h3 className="text-lg font-bold text-foreground">The Builder&rsquo;s Decision Loop</h3>
              <p className="text-sm text-muted-foreground">
                You have a project idea and need to know if the space is saturated, who the dominant competitors are, and where the uncovered opportunities lie.
              </p>

              <div className="space-y-2 pt-2 text-xs">
                <div className="flex items-center gap-2 text-foreground font-medium">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-[11px]">
                    1
                  </span>
                  <span>Formulate multi-angle search queries across GitHub</span>
                </div>
                <div className="flex items-center gap-2 text-foreground font-medium">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-[11px]">
                    2
                  </span>
                  <span>Extract embeddings & compute cosine distance to candidates</span>
                </div>
                <div className="flex items-center gap-2 text-foreground font-medium">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-[11px]">
                    3
                  </span>
                  <span>Cluster repositories by shared topics & tech stack</span>
                </div>
                <div className="flex items-center gap-2 text-foreground font-medium">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-[11px]">
                    4
                  </span>
                  <span>Evaluate deterministic novelty score (30/25/20/15/10 weights)</span>
                </div>
              </div>
            </div>

            <div className="mt-6 pt-4 border-t flex items-center justify-between">
              <span className="text-xs text-muted-foreground">Validate &rarr; Ecosystem &rarr; Build</span>
              <Link
                to="/validate"
                className="inline-flex items-center gap-1.5 text-xs font-semibold text-primary hover:underline"
              >
                <span>Start Validation</span>
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </div>
          </div>

          {/* Journey 2: The Portfolio Gap Loop */}
          <div className="flex flex-col justify-between rounded-xl border bg-card p-6 shadow-sm">
            <div className="space-y-3">
              <div className="inline-flex items-center gap-2 rounded-md bg-blue-500/10 px-2.5 py-1 text-xs font-semibold text-blue-700 dark:text-blue-400 border border-blue-500/20">
                Secondary Growth Journey
              </div>
              <h3 className="text-lg font-bold text-foreground">The Portfolio Gap Loop</h3>
              <p className="text-sm text-muted-foreground">
                You want your next open-source project to demonstrate missing capabilities, bridge portfolio gaps, and strengthen evidence for your career goals.
              </p>

              <div className="space-y-2 pt-2 text-xs">
                <div className="flex items-center gap-2 text-foreground font-medium">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-blue-500/10 text-blue-600 dark:text-blue-400 font-bold text-[11px]">
                    1
                  </span>
                  <span>Analyze public GitHub repos against taxonomy-v1</span>
                </div>
                <div className="flex items-center gap-2 text-foreground font-medium">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-blue-500/10 text-blue-600 dark:text-blue-400 font-bold text-[11px]">
                    2
                  </span>
                  <span>Group capabilities into Strong, Moderate, and Limited evidence</span>
                </div>
                <div className="flex items-center gap-2 text-foreground font-medium">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-blue-500/10 text-blue-600 dark:text-blue-400 font-bold text-[11px]">
                    3
                  </span>
                  <span>Calculate marginal value of candidate project concepts</span>
                </div>
                <div className="flex items-center gap-2 text-foreground font-medium">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-blue-500/10 text-blue-600 dark:text-blue-400 font-bold text-[11px]">
                    4
                  </span>
                  <span>Recommend 3 diverse, high-opportunity projects</span>
                </div>
              </div>
            </div>

            <div className="mt-6 pt-4 border-t flex items-center justify-between">
              <span className="text-xs text-muted-foreground">Portfolio &rarr; Gaps &rarr; Next Project</span>
              <Link
                to="/portfolio"
                className="inline-flex items-center gap-1.5 text-xs font-semibold text-primary hover:underline"
              >
                <span>Analyze Portfolio</span>
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* Feature Grid: Pillar 1 & Pillar 2 */}
      <section className="space-y-4">
        <h2 className="text-xl font-bold tracking-tight text-foreground">
          Platform Capabilities
        </h2>

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Link
            to="/validate"
            className="group rounded-xl border bg-card p-5 transition-all hover:border-primary/50 hover:shadow-sm"
          >
            <div className="flex items-center justify-between">
              <div className="rounded-lg bg-primary/10 p-2 text-primary">
                <Lightbulb className="h-5 w-5" />
              </div>
              <ArrowRight className="h-4 w-4 text-muted-foreground group-hover:translate-x-0.5 group-hover:text-primary transition-all" />
            </div>
            <h3 className="mt-3 font-semibold text-base text-foreground">Idea Validation</h3>
            <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
              Scan candidate repositories, identify direct competitors, and compute deterministic novelty scores.
            </p>
          </Link>

          <Link
            to="/ecosystem"
            className="group rounded-xl border bg-card p-5 transition-all hover:border-primary/50 hover:shadow-sm"
          >
            <div className="flex items-center justify-between">
              <div className="rounded-lg bg-emerald-500/10 p-2 text-emerald-600 dark:text-emerald-400">
                <Network className="h-5 w-5" />
              </div>
              <ArrowRight className="h-4 w-4 text-muted-foreground group-hover:translate-x-0.5 group-hover:text-primary transition-all" />
            </div>
            <h3 className="mt-3 font-semibold text-base text-foreground">Ecosystem Intelligence</h3>
            <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
              Map connected components, cluster boundaries, activity signals, and concentration across technical domains.
            </p>
          </Link>

          <Link
            to="/recommend"
            className="group rounded-xl border bg-card p-5 transition-all hover:border-primary/50 hover:shadow-sm"
          >
            <div className="flex items-center justify-between">
              <div className="rounded-lg bg-purple-500/10 p-2 text-purple-600 dark:text-purple-400">
                <Sparkles className="h-5 w-5" />
              </div>
              <ArrowRight className="h-4 w-4 text-muted-foreground group-hover:translate-x-0.5 group-hover:text-primary transition-all" />
            </div>
            <h3 className="mt-3 font-semibold text-base text-foreground">Find Next Project</h3>
            <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
              Discover 3 distinct candidate projects ranked by career goal alignment and marginal portfolio value.
            </p>
          </Link>

          <Link
            to="/discover"
            className="group rounded-xl border bg-card p-5 transition-all hover:border-primary/50 hover:shadow-sm"
          >
            <div className="flex items-center justify-between">
              <div className="rounded-lg bg-blue-500/10 p-2 text-blue-600 dark:text-blue-400">
                <Compass className="h-5 w-5" />
              </div>
              <ArrowRight className="h-4 w-4 text-muted-foreground group-hover:translate-x-0.5 group-hover:text-primary transition-all" />
            </div>
            <h3 className="mt-3 font-semibold text-base text-foreground">Explore GitHub</h3>
            <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
              Query live GitHub APIs and trigger background enrichment workers to parse READMEs, topics, and languages.
            </p>
          </Link>

          <Link
            to="/search"
            className="group rounded-xl border bg-card p-5 transition-all hover:border-primary/50 hover:shadow-sm"
          >
            <div className="flex items-center justify-between">
              <div className="rounded-lg bg-amber-500/10 p-2 text-amber-600 dark:text-amber-400">
                <Search className="h-5 w-5" />
              </div>
              <ArrowRight className="h-4 w-4 text-muted-foreground group-hover:translate-x-0.5 group-hover:text-primary transition-all" />
            </div>
            <h3 className="mt-3 font-semibold text-base text-foreground">Search RepoLens</h3>
            <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
              Hybrid search combining PostgreSQL Full Text Search and pgvector embeddings using reciprocal rank fusion.
            </p>
          </Link>

          <Link
            to="/portfolio"
            className="group rounded-xl border bg-card p-5 transition-all hover:border-primary/50 hover:shadow-sm"
          >
            <div className="flex items-center justify-between">
              <div className="rounded-lg bg-rose-500/10 p-2 text-rose-600 dark:text-rose-400">
                <UserCheck className="h-5 w-5" />
              </div>
              <ArrowRight className="h-4 w-4 text-muted-foreground group-hover:translate-x-0.5 group-hover:text-primary transition-all" />
            </div>
            <h3 className="mt-3 font-semibold text-base text-foreground">Portfolio Analysis</h3>
            <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
              Audit public repositories to classify evidence into taxonomy-v1 categories and identify strategic gaps.
            </p>
          </Link>
        </div>
      </section>

      {/* Trust & Methodology Manifesto */}
      <section className="rounded-xl border border-dashed bg-muted/20 p-6">
        <div className="flex items-center gap-2">
          <ShieldCheck className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
          <h3 className="font-bold text-base text-foreground">Evidence, Not Vibes: Trust & Methodology</h3>
        </div>
        <p className="mt-2 text-xs text-muted-foreground leading-relaxed">
          RepoLens adheres to four strict architectural constraints to ensure every recommendation and score is grounded in verifiable evidence:
        </p>

        <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4 text-xs">
          <div className="space-y-1">
            <div className="flex items-center gap-1.5 font-semibold text-foreground">
              <Cpu className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
              <span>Deterministic Formulas</span>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              Scoring algorithms use versioned mathematical formulas (<code className="font-mono text-[10px]">novelty-v1</code>, <code className="font-mono text-[10px]">marginal-v1</code>). LLMs never compute scores.
            </p>
          </div>

          <div className="space-y-1">
            <div className="flex items-center gap-1.5 font-semibold text-foreground">
              <Database className="h-4 w-4 text-blue-600 dark:text-blue-400" />
              <span>Bounded Candidate Sets</span>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              All claims explicitly state candidate pool sizes. RepoLens analyzes gathered candidate sets &mdash; it does not claim to index all of GitHub.
            </p>
          </div>

          <div className="space-y-1">
            <div className="flex items-center gap-1.5 font-semibold text-foreground">
              <Layers className="h-4 w-4 text-purple-600 dark:text-purple-400" />
              <span>Inspectable Citations</span>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              Every competitor, cluster member, and search result links to its live GitHub repository and local enrichment detail.
            </p>
          </div>

          <div className="space-y-1">
            <div className="flex items-center gap-1.5 font-semibold text-foreground">
              <Sparkles className="h-4 w-4 text-amber-600 dark:text-amber-400" />
              <span>Strict LLM Boundaries</span>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              LLMs assist exclusively with query expansion and project ideation with deterministic fallbacks. Never hallucinations as evidence.
            </p>
          </div>
        </div>
      </section>

      {/* Footer / Status */}
      <div className="flex items-center justify-between text-xs text-muted-foreground border-t pt-4">
        <span>PostgreSQL 17 + pgvector &bull; .NET 10 Minimal API &bull; React 19</span>
        <div className="flex items-center gap-2">
          <span>Backend API Status:</span>
          <ApiStatusBadge />
        </div>
      </div>
    </div>
  )
}
