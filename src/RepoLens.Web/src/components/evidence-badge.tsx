import { Calculator, Sparkles, Database, ShieldCheck } from 'lucide-react'
import { cn } from '@/lib/utils'

export type EvidenceType = 'deterministic' | 'llm-assisted' | 'candidate-scoped' | 'hybrid'

interface EvidenceBadgeProps {
  type: EvidenceType
  label: string
  subtext?: string
  version?: string
  className?: string
}

export function EvidenceBadge({
  type,
  label,
  subtext,
  version,
  className,
}: EvidenceBadgeProps) {
  const config = {
    deterministic: {
      icon: Calculator,
      badgeClass: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 border-emerald-500/20',
      iconClass: 'text-emerald-600 dark:text-emerald-400',
      prefix: 'Deterministic',
    },
    'llm-assisted': {
      icon: Sparkles,
      badgeClass: 'bg-purple-500/10 text-purple-700 dark:text-purple-400 border-purple-500/20',
      iconClass: 'text-purple-600 dark:text-purple-400',
      prefix: 'LLM Assisted',
    },
    'candidate-scoped': {
      icon: Database,
      badgeClass: 'bg-blue-500/10 text-blue-700 dark:text-blue-400 border-blue-500/20',
      iconClass: 'text-blue-600 dark:text-blue-400',
      prefix: 'Scoped Index',
    },
    hybrid: {
      icon: ShieldCheck,
      badgeClass: 'bg-amber-500/10 text-amber-700 dark:text-amber-400 border-amber-500/20',
      iconClass: 'text-amber-600 dark:text-amber-400',
      prefix: 'Hybrid RRF',
    },
  }[type]

  const Icon = config.icon

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium transition-colors',
        config.badgeClass,
        className
      )}
      title={subtext ?? `${config.prefix}: ${label}`}
    >
      <Icon className={cn('h-3.5 w-3.5 shrink-0', config.iconClass)} />
      <span>{label}</span>
      {version && (
        <span className="rounded bg-background/50 px-1 py-0.2 text-[10px] font-mono opacity-80">
          {version}
        </span>
      )}
    </span>
  )
}
