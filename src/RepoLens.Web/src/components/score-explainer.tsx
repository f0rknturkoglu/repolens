import { useState } from 'react'
import { ChevronDown, ChevronUp, Info, ShieldCheck } from 'lucide-react'
import { cn } from '@/lib/utils'
import { EvidenceBadge } from './evidence-badge'

export interface ScoreComponent {
  name: string
  weightPercent: number
  score: number // 0 to 1
  description: string
}

interface ScoreExplainerProps {
  score: number // 0 to 1 or 0 to 100
  formula: 'novelty-v1' | 'marginal-v1' | 'rec-v1' | 'taxonomy-v1'
  band?: string
  candidateCount?: number
  components?: ScoreComponent[]
  title?: string
  description?: string
  className?: string
}

export function ScoreExplainer({
  score,
  formula,
  band,
  candidateCount,
  components,
  title = 'Opportunity Score',
  description,
  className,
}: ScoreExplainerProps) {
  const [showDetails, setShowDetails] = useState(false)

  // Normalize score to 0..100
  const normalizedScore = score <= 1.0 ? Math.round(score * 100) : Math.round(score)

  const getScoreColor = (val: number) => {
    if (val >= 70) return { text: 'text-emerald-600 dark:text-emerald-400', bg: 'bg-emerald-500', badge: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/20' }
    if (val >= 40) return { text: 'text-amber-600 dark:text-amber-400', bg: 'bg-amber-500', badge: 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/20' }
    return { text: 'text-slate-600 dark:text-slate-400', bg: 'bg-slate-500', badge: 'bg-slate-500/10 text-slate-700 dark:text-slate-300 border-slate-500/20' }
  }

  const colors = getScoreColor(normalizedScore)

  return (
    <div className={cn('rounded-xl border bg-card p-5 text-card-foreground shadow-sm', className)}>
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-lg tracking-tight">{title}</h3>
            <EvidenceBadge
              type="deterministic"
              label={formula}
              subtext="Computed with deterministic formula weights; no LLM estimation"
            />
          </div>
          {description && (
            <p className="mt-1 text-sm text-muted-foreground">{description}</p>
          )}
        </div>

        <div className="flex items-center gap-3">
          {band && (
            <span
              className={cn(
                'rounded-full border px-3 py-1 text-xs font-semibold tracking-wide uppercase',
                colors.badge
              )}
            >
              {band}
            </span>
          )}
          <div className="flex items-baseline gap-1">
            <span className={cn('text-3xl font-extrabold tracking-tight', colors.text)}>
              {normalizedScore}
            </span>
            <span className="text-sm font-medium text-muted-foreground">/100</span>
          </div>
        </div>
      </div>

      {/* Visual progress track */}
      <div
        role="progressbar"
        aria-valuenow={normalizedScore}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`${title}: ${normalizedScore} out of 100`}
        className="mt-4 h-2 w-full overflow-hidden rounded-full bg-muted"
      >
        <div
          className={cn('h-full transition-all duration-500 ease-out', colors.bg)}
          style={{ width: `${Math.min(100, Math.max(0, normalizedScore))}%` }}
        />
      </div>

      {/* Candidate scope boundary disclosure */}
      <div className="mt-4 flex items-start gap-2 rounded-lg bg-muted/50 p-3 text-xs text-muted-foreground">
        <Info className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
        <div>
          <span className="font-semibold text-foreground">Candidate set boundary: </span>
          {candidateCount !== undefined ? (
            <>
              Evaluated across <strong>{candidateCount} candidate repositories</strong> gathered
              through systematic multi-query GitHub search.
            </>
          ) : (
            <>Evaluated across retrieved candidate repositories in the indexed search space.</>
          )}{' '}
          This is an evidence-bounded calculation, not a guarantee over all unindexed repositories.
        </div>
      </div>

      {/* Breakdown toggle */}
      {(components && components.length > 0) && (
        <div className="mt-4 border-t pt-3">
          <button
            type="button"
            onClick={() => setShowDetails(!showDetails)}
            className="flex w-full items-center justify-between text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
          >
            <span className="flex items-center gap-1.5">
              <ShieldCheck className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
              <span>Formula breakdown & weights ({formula})</span>
            </span>
            {showDetails ? (
              <ChevronUp className="h-4 w-4" />
            ) : (
              <ChevronDown className="h-4 w-4" />
            )}
          </button>

          {showDetails && (
            <div className="mt-3 space-y-3 pt-2">
              {components.map((comp) => {
                const compScorePct = Math.round(comp.score * 100)
                return (
                  <div key={comp.name} className="space-y-1">
                    <div className="flex items-center justify-between text-xs">
                      <span className="font-medium text-foreground">
                        {comp.name}{' '}
                        <span className="text-muted-foreground">({comp.weightPercent}% weight)</span>
                      </span>
                      <span className="font-mono text-xs font-semibold">{compScorePct}/100</span>
                    </div>
                    <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
                      <div
                        className="h-full bg-primary/70 transition-all"
                        style={{ width: `${Math.min(100, Math.max(0, compScorePct))}%` }}
                      />
                    </div>
                    <p className="text-[11px] text-muted-foreground">{comp.description}</p>
                  </div>
                )
              })}

              <div className="mt-3 rounded border border-dashed p-2.5 text-[11px] text-muted-foreground">
                <span className="font-medium text-foreground">Methodology Note: </span>
                Formula strictly combines closest candidate distance, similarity density, cluster dominance, active competition, and near-duplicate scarcity. Scores are deterministic and reproducible given the same candidate set.
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
