# Search & ranking

## Repository similarity (ecosystem analysis, similarity evidence)

`score(a,b) = 0.5·Dice(topics) + 0.3·Dice(name tokens) + 0.2·sameLanguage`,
with empty dimensions excluded and the remainder renormalized. Name tokens
lowercase the name, split on `- _ . space` and camelCase boundaries, and drop
stop words (tool, lib, client, …). Edges exist at ≥ 0.30; clusters are the
connected components (ADR 002). Similarity shown on "similar projects" uses the
vector cosine distance `sim = 1 − cosine_distance/2` (0..1).

## Full-text index

One `search_documents` row per enriched repository:

| Column | Weight | Notes |
| --- | --- | --- |
| `name` | A | `setweight(to_tsvector('english', …), 'A')` |
| `topics` (space-joined) | B | |
| `description` | C | |
| `readme_text` (normalized) | D | |

`search_vector` is a PostgreSQL generated column (sum of the four weighted
vectors) with a GIN index. Keyword ranking uses `ts_rank_cd` with weights
`{0.1, 0.2, 0.4, 1.0}` (D..A) — name/topic hits outrank README-only hits.
Queries are parsed with `plainto_tsquery('english', …)` (AND semantics).

## Vector index

Embeddings are stored in the same row (`embedding vector`, model + content hash
alongside). A vector is **stale** when its hash differs from the document
content hash or its model differs from the configured model — the worker
backfills stale vectors on a bounded budget. Search scopes vector legs to rows
of the current model. No HNSW/IVFFlat index yet: the dataset is small and exact
cosine search is measured before adding an index (roadmap Phase 3).

## Hybrid merge: Reciprocal Rank Fusion

`GET /api/search/repositories` runs two legs:

1. keyword (FTS, above), up to 100 results;
2. vector (exact cosine, nearest first), up to 100 results — only when an
   embedding provider is configured; otherwise the response reports
   `method: Keyword` with a fallback reason.

Both legs apply the same filters (language, archived, min stars). Results are
merged with RRF: `score_i = Σ k / (k + rank_in_leg)` with `k = 60`; ties break by
repository id; the same repository in both legs is a single result with its
best rank recorded. Scores are normalized by the maximum (best hit = 1.0).
Deterministic, no LLM, documented weights.
