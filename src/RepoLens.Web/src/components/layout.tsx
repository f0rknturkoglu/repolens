import { Link, NavLink } from 'react-router-dom'
import { Sparkles, Compass, Search, Lightbulb, Network, UserCheck } from 'lucide-react'
import { cn } from '@/lib/utils'

export function AppLayout({ children }: { children: React.ReactNode }) {
  const navItemClass = ({ isActive }: { isActive: boolean }) =>
    cn(
      'flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-medium transition-colors',
      isActive
        ? 'bg-primary/10 text-primary font-semibold'
        : 'text-muted-foreground hover:bg-muted hover:text-foreground'
    )

  return (
    <div className="flex min-h-svh flex-col bg-background text-foreground">
      <header className="sticky top-0 z-40 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80">
        <div className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex items-center gap-6">
            <Link to="/" className="flex items-center gap-2 group">
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground font-bold shadow-xs">
                RL
              </div>
              <div className="flex flex-col">
                <span className="font-bold text-sm tracking-tight leading-none group-hover:text-primary transition-colors">
                  RepoLens
                </span>
                <span className="text-[10px] text-muted-foreground leading-tight tracking-wider uppercase">
                  Ecosystem Intelligence
                </span>
              </div>
            </Link>

            {/* Navigation grouped into two crisp product pillars */}
            <nav className="hidden md:flex items-center gap-6 text-sm">
              {/* Pillar 1: Decision Loop */}
              <div className="flex items-center gap-1 border-l pl-4">
                <span className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground/70 mr-1.5 select-none">
                  Decide
                </span>
                <NavLink to="/validate" className={navItemClass}>
                  <Lightbulb className="h-3.5 w-3.5" />
                  <span>Validate Idea</span>
                </NavLink>
                <NavLink to="/ecosystem" className={navItemClass}>
                  <Network className="h-3.5 w-3.5" />
                  <span>Ecosystem</span>
                </NavLink>
                <NavLink to="/recommend" className={navItemClass}>
                  <Sparkles className="h-3.5 w-3.5" />
                  <span>Next Project</span>
                </NavLink>
              </div>

              {/* Pillar 2: Research & Signals */}
              <div className="flex items-center gap-1 border-l pl-4">
                <span className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground/70 mr-1.5 select-none">
                  Research
                </span>
                <NavLink to="/discover" className={navItemClass}>
                  <Compass className="h-3.5 w-3.5" />
                  <span>Explore GitHub</span>
                </NavLink>
                <NavLink to="/search" className={navItemClass}>
                  <Search className="h-3.5 w-3.5" />
                  <span>Search RepoLens</span>
                </NavLink>
                <NavLink to="/portfolio" className={navItemClass}>
                  <UserCheck className="h-3.5 w-3.5" />
                  <span>Portfolio</span>
                </NavLink>
              </div>
            </nav>
          </div>

          <div className="flex items-center gap-3">
            <Link
              to="/validate"
              className="hidden sm:inline-flex items-center justify-center rounded-md bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground shadow-xs hover:bg-primary/90 transition-colors"
            >
              Validate an Idea
            </Link>
          </div>
        </div>

        {/* Mobile Pillar Bar */}
        <div className="flex md:hidden border-t px-4 py-2 overflow-x-auto gap-2 text-xs">
          <NavLink to="/validate" className={navItemClass}>
            <Lightbulb className="h-3.5 w-3.5" />
            <span>Validate</span>
          </NavLink>
          <NavLink to="/ecosystem" className={navItemClass}>
            <Network className="h-3.5 w-3.5" />
            <span>Ecosystem</span>
          </NavLink>
          <NavLink to="/recommend" className={navItemClass}>
            <Sparkles className="h-3.5 w-3.5" />
            <span>Recommend</span>
          </NavLink>
          <NavLink to="/discover" className={navItemClass}>
            <Compass className="h-3.5 w-3.5" />
            <span>Explore</span>
          </NavLink>
          <NavLink to="/search" className={navItemClass}>
            <Search className="h-3.5 w-3.5" />
            <span>Search</span>
          </NavLink>
          <NavLink to="/portfolio" className={navItemClass}>
            <UserCheck className="h-3.5 w-3.5" />
            <span>Portfolio</span>
          </NavLink>
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8 sm:px-6">{children}</main>

      <footer className="border-t bg-muted/20 py-6 text-xs text-muted-foreground">
        <div className="mx-auto flex max-w-6xl flex-col sm:flex-row items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex items-center gap-2">
            <span className="font-semibold text-foreground">RepoLens</span>
            <span>—</span>
            <span>Evidence-backed GitHub ecosystem intelligence. Not vibes, not guesswork.</span>
          </div>
          <div className="flex items-center gap-4">
            <a
              href="/health"
              target="_blank"
              rel="noreferrer"
              className="flex items-center gap-1.5 hover:text-foreground transition-colors"
            >
              <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
              <span>API Health: Online</span>
            </a>
            <span className="text-muted-foreground/50">•</span>
            <span>Deterministic Scoring Engines: novelty-v1 &bull; marginal-v1</span>
          </div>
        </div>
      </footer>
    </div>
  )
}

export function PageHeader({
  title,
  subtitle,
  action,
}: {
  title: string
  subtitle?: string
  action?: React.ReactNode
}) {
  return (
    <div className="mb-6 flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b pb-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-foreground">{title}</h1>
        {subtitle ? <p className="mt-1 text-sm text-muted-foreground">{subtitle}</p> : null}
      </div>
      {action ? <div className="shrink-0">{action}</div> : null}
    </div>
  )
}
