# ADR 002: Deterministic similarity-graph clustering for ecosystem analysis

Status: accepted

Date: 2026-09-02

## Context

Ecosystem analysis (docs/product.md) groups candidate repositories into "product
families" so a user can see how a project domain is shaped — dense clusters of
near-duplicates vs. a spread of distinct approaches. The grouping must be:

- deterministic (the same candidates always produce the same groups);
- explainable (every edge and group is derivable from stored repository
  metadata, shown to the user as evidence);
- reproducible (an analysis session stores its snapshot, so a later report does
  not silently depend on embedding drift);
- lightweight (no extra runtime, model files, or services);
- robust without an embedding provider configured.

## Decision

Cluster candidates with **connected components of a similarity graph**:

1. Similarity between two repositories is a weighted, renormalized blend of
   topic overlap (Dice), name-token overlap (Dice), and same primary language.
   Weight 0.5 / 0.3 / 0.2; empty dimensions are excluded. No LLM and no
   embeddings are involved — similarity is a pure function of the enriched
   repository record (docs/analysis-limitations.md).
2. An undirected edge exists when similarity ≥ 0.30.
3. Clusters are the connected components of that graph, computed with BFS over
   id-ordered adjacency (deterministic output order, transitive grouping).
4. Per cluster, members are ordered by centrality (mean similarity to the other
   members) and labelled heuristically (majority language or a dominant topic,
   else "Cluster N" / "Standalone").

The threshold, weights, and algorithm are versioned with the analysis engine
(`EcosystemAnalysis.Version`) so future tuning never rewrites history.

## Why not alternatives

| Option | Rejected because |
| --- | --- |
| Python + HDBSCAN | Adds a second runtime + service boundary to the modular monolith, requires shipping models/tuning, is non-deterministic across runs, and its density clusters are hard to explain with plain evidence. No measured need for density-based clusters at the bounded candidate scale (≤ ~90 repos). |
| ML.NET clustering | Adds a heavy dependency; algorithm choice (k-means) requires k and is sensitive to scaling; less explainable per-edge grouping. |
| Vector-embedding clustering | Requires an embedding provider to be configured; results drift when the model changes; contradicts the "works without provider, evidence-based" goal for analysis (embeddings remain available for hybrid *search* where they are the point). |

A threshold-based graph was chosen because it yields the exact artifact the UI
shows — "these repositories form a family" with visible, citable reasons
(shared topics/language/name) — at bounded cost with no new infrastructure.

## Consequences

- Clustering is deterministic and unit-testable without a database.
- Analysis runs are cheap enough to run synchronously per request.
- Graph clustering cannot find density variations *within* a connected blob
  (e.g. two product families that share enough topics to bridge); near-duplicate
  detection remains visible via similarity density metrics. If the product later
  needs finer grouping, this ADR records the constraints and an HDBSCAN-style
  approach can be reconsidered with real evidence of need.
