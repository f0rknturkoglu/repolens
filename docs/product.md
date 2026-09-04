# Product

RepoLens is **evidence-backed GitHub ecosystem intelligence for developers deciding what to build next**. It surveys public GitHub repositories and answers product decisions with mathematical evidence — verifiable numbers, formula breakdowns, and repository citations — instead of vibes.

## Core User Journeys

### 1. Primary Decision Journey: The Builder's Loop
```
Idea Concept → Validate Idea (/validate) → Understand Ecosystem (/ecosystem) → Inspect Competitors (/repositories/:id) → Decide What to Build
```
- **Entry point:** Builder has a concept (e.g. *"CLI for zero-downtime postgres migrations"*).
- **Validation:** Multi-query search plan finds competitors; pgvector computes cosine distances; deterministic `novelty-v1` computes an opportunity score (30% closest distance, 25% density, 20% cluster share, 15% active competition, 10% near-duplicate scarcity).
- **Action Bridges:** Direct one-click jump to `/ecosystem?query=...` to see market clustering or `/recommend?goal=...` to discover adjacent gaps.

### 2. Secondary Growth Journey: The Portfolio Gap Loop
```
GitHub Profile → Portfolio Analysis (/portfolio) → Identify Limited Evidence Gaps → Find Next Project (/recommend) → High Marginal Value Project
```
- **Entry point:** Developer wants to know what their public repositories demonstrate.
- **Audit:** Maps repos to `taxonomy-v1` categories into Strong, Moderate, and Limited evidence bands.
- **Action Bridges:** From any "Limited Evidence" area, click **"Bridge Gap"** to auto-fill `/recommend` with that domain, returning 3 GitHub-checked projects ranked by `rec-v1` marginal portfolio value.

## Two-Pillar Navigation Structure

1. **Decide What to Build:**
   - **Validate Idea** (`/validate`) — Idea saturation, closest competitors, observed gaps.
   - **Ecosystem Landscape** (`/ecosystem`) — Connected components, tech stack shares, market concentration.
   - **Find Next Project** (`/recommend`) — 3 distinct candidates ranked deterministically.
2. **Explore & Research:**
   - **Explore GitHub** (`/discover`) — Query live GitHub and trigger background enrichment workers.
   - **Search RepoLens Index** (`/search`) — PostgreSQL FTS + pgvector hybrid reciprocal rank fusion (k=60).
   - **Developer Portfolio** (`/portfolio`) — Map capability signals and compute marginal idea value.

## Principles & Trust Architecture

- **Deterministic & Versioned Formulas:** `novelty-v1`, `marginal-v1`, `rec-v1`, and `taxonomy-v1` use explicit mathematical weights. No LLM computes scores or hallucinates metrics.
- **Bounded Candidate Sets:** Every claim states candidate set boundaries (e.g. *"Evaluated across 48 candidate repositories gathered via search plan"*). Never claims to index all 400M+ GitHub repositories.
- **Inspectable Citations:** Competitors and cluster members link directly to GitHub and to local enrichment details.
- **Strict LLM Boundaries:** LLMs suggest search queries and brainstorm project candidates; all rankings, similarities, and scores are 100% deterministic with robust offline fallbacks.

