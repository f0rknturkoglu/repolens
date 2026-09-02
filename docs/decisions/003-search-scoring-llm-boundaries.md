# ADR 003: Search, scoring, and LLM boundaries

Status: accepted

Date: 2026-09-02

## Context

As features landed (Phases 3–8) RepoLens chose concrete algorithms for hybrid
retrieval, deterministic novelty scoring, portfolio signal taxonomy, and
recommendation ranking, and it bounded what LLMs are allowed to do. These
decisions are used across the codebase; this ADR records the reasoning once.

## Decisions

### 1. Hybrid retrieval = FTS + exact pgvector + Reciprocal Rank Fusion

- Keyword leg: PostgreSQL full-text (`tsvector` generated column, weighted
  name A / topics B / description C / README D, GIN index, `ts_rank_cd`).
- Vector leg: exact cosine distance over stored embeddings
  (`embedding vector`, model + content hash tracking staleness).
- Merge: RRF with k = 60, id tie-break, normalized scores (docs/search-ranking.md).
- No HNSW/IVFFlat index yet: the dataset is small; exact search is measured
  before adding index structure. Embedding provider/model are configuration;
  without them search degrades to keyword with an explicit fallback reason.

Why: both legs are plain SQL/pure functions, deterministic, unit-testable, and
documented. A dedicated search service (Elasticsearch) was rejected until the
data grows past a single PostgreSQL instance (no measured need; roadmap Phase 8
explicitly forbids speculative infra).

### 2. Deterministic novelty scoring (`novelty-v1`)

Novelty is a weighted combination (0.30 closest-competitor distance, 0.25
similarity density, 0.20 largest-cluster share, 0.15 active-competitor
scarcity, 0.10 near-duplicate scarcity) computed purely over the analyzed
candidate set, with per-component evidence and a versioned formula. It is a
heuristic, never a market claim. Why: reproducibility, explainability, and
testability; a "novelty" number no one can derive from the inputs would be
worse than no number.

### 3. LLM boundaries

The LLM may: suggest search queries (validated, bounded), brainstorm candidate
project ideas (validated, bounded, deterministic fallback bank), and — in later
iterations — phrase explanations over precomputed evidence. The LLM may never:
compute similarity, novelty, feasibility, portfolio signals, or final ranking;
invent competitors; or claim absence. Every LLM path has a deterministic
fallback and malformed/unavailable output is tested. Why: scores must stay
auditable and cheap to re-run; the GitHub evidence is the product, prose is a
surface.

### 4. Portfolio signal taxonomy (`taxonomy-v1`)

Bounded, versioned, config-shaped category list (11 categories; language sets +
keyword concepts). Evidence extraction is deterministic: one evidence item per
repository per category with a concrete reason; bands strong/moderate/limited by
repository count. UI language is "portfolio signals"/"evidence", never
"skills"/grades. Why: reproducible reports, evidence traceability, and honest
claims about what public metadata can show.

### 5. Recommendation ranking (`rec-v1`)

Final ranking is a weighted deterministic formula (originality 0.30, portfolio
marginal value 0.20, feasibility 0.20, goal alignment 0.15, interest alignment
0.15) with category diversity over the top 3 and +/- explainability lines.
Feasibility is a relative scope estimate from text signals. The LLM generates
candidates only; it never overrides the ranking. Why: users must be able to
understand and challenge any recommendation, and regression fixtures can assert
deterministic properties of the ranking.

## Consequences

- Each formula is versioned and stored with the analysis snapshot; changing
  weights/taxonomy never rewrites history.
- All ranking logic is unit-testable without a database or network.
- UI shows coarse, honest numbers and candidate-scoped claims; no false
  precision and no absolute statements about GitHub.
- Future density-clustering or learned-ranking needs a new ADR with measured
  evidence; see ADR 002 for the clustering rationale.
