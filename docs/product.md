# Product

RepoLens is GitHub Ecosystem Intelligence: it reads public GitHub repositories
and answers product questions about them with **evidence** — numbers and
repository citations — instead of vibes.

## Capabilities

| Flow | What it answers | Evidence model |
| --- | --- | --- |
| Explore GitHub | "Find repositories on GitHub." | discovery results persisted locally by numeric GitHub id, auto-enriched |
| Repository detail | "What is this repo really?" | README, topics, language distribution, metrics + snapshots, enrichment status |
| Search RepoLens | "What did *I* collect?" | hybrid keyword + semantic search over RepoLens' own index (RRF) |
| Similar projects | "What else is like this?" | vector similarity + shared topic/language reasons |
| Ecosystem analysis | "How does this whole project domain look?" | deterministic clusters + metrics with per-number evidence |
| Validate an Idea | "Is my idea already done?" | Estimated Novelty (heuristic, versioned), closest competitors, observed gaps — scoped to the analyzed candidate set |
| Analyze Portfolio | "What does my public profile evidence?" | portfolio **signals** per bounded taxonomy, coverage bands, per-repo evidence (no skill grading) |
| Find My Next Project | "Which project fits my profile and goal?" | 3 diverse, GitHub-validated candidates ranked deterministically with +/- reasons |

## Principles

- Every number shown is derivable from stored evidence and re-runnable.
- "Observed gaps"/novelty statements are scoped to the analyzed candidate set —
  never claims about all of GitHub.
- LLMs brainstorm and phrase; they never compute scores and never block the
  pipeline (deterministic fallbacks everywhere).
- No fake precision: novelty/feasibility/similarity are shown with simple,
  coarse presentation.
- Identity (GitHub OAuth) is optional; all flows work anonymously.

## Product language

Portfolio analysis produces "signals"/"evidence" and coverage bands
(strong / moderate / limited) — never skill verdicts like "you don't know X".
Feasibility is "relative scope estimate (Small…Very Large)", not a schedule.
Novelty is "Estimated Novelty — heuristic, not a market claim".
