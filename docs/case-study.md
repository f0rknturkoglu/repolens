# Technical Case Study: RepoLens — Evidence-Backed GitHub Ecosystem Intelligence

## Executive Summary

**RepoLens** helps software engineers investigate "What should I build next?" with bounded GitHub evidence instead of an unqualified novelty claim.

RepoLens implements a **deterministic intelligence engine** atop an ASP.NET Core 10 modular monolith and PostgreSQL 17 with `pgvector`. It replaces black-box LLM hallucinations with versioned mathematical formulas, hybrid reciprocal rank fusion (RRF), graph-based connected components clustering, and a durable background worker.

---

## The Problem & Engineering Motivation

Every week, developers and research engineers start side projects, open-source libraries, or developer tools without understanding the competitive landscape:
1. **Star Count Fallacy:** GitHub stars reflect historical hype, not active maintenance or unmet architectural niches. A library with 15k stars might be unmaintained for 3 years, leaving a prime opportunity for a modern alternative.
2. **LLM Hallucination & Flattery:** General-purpose AI output can praise an idea without first inspecting relevant repositories or identifying existing solutions.
3. **Black-Box Metrics:** Most market-research tools provide vague percentages without mathematical proof or reproducible candidate sets.

RepoLens enforces **"Conclusion → Why → Evidence → Methodology"**: every claim is bounded to an explicitly gathered candidate set ($N$ repositories), scored with versioned mathematical equations (`novelty-v1`, `rec-v1`, `marginal-v1`), and backed by verifiable repository metadata.

---

## System Architecture & Data Pipeline

RepoLens is intentionally engineered as a **modular monolith** with a single production database (`PostgreSQL 17 + pgvector`). External integrations (GitHub API, LLM providers) are isolated behind outbound ports at the architecture edge.

```mermaid
graph TD
    subgraph "Client Layer (React 19 + TypeScript + Vite)"
        UI["Evidence-First Web Interface<br/>• Decision Pillars (Decide vs Explore)<br/>• ScoreExplainer & EvidenceBadges<br/>• Zod Contract Schemas"]
    end

    subgraph "ASP.NET Core 10 Monolith Edge"
        API["Minimal APIs & Middleware<br/>• RateLimiting (300/min global, 20/min expensive)<br/>• ExceptionHandler (GitHub 403/429/5xx mapping)<br/>• OpenTelemetry Tracing & Metrics"]
    end

    subgraph "Core Domain & Application Engines"
        HybridSearch["Hybrid Search Engine<br/>• tsvector FTS (English weights A/B/C/D)<br/>• pgvector cosine distance (1536-dim)<br/>• Reciprocal Rank Fusion (k=60)"]
        Clusterizer["Ecosystem Graph Engine<br/>• Token & Language Jaccard/Dice edges<br/>• Connected Components BFS (deterministic)<br/>• Centrality-ranked archetypes"]
        ScoringEngine["Deterministic Scoring Core<br/>• novelty-v1 (30/25/20/15/10 weights)<br/>• rec-v1 (30/20/20/15/15 weights)<br/>• marginal-v1 (Coverage taxonomy)"]
        Worker["Durable Background Worker<br/>• FOR UPDATE SKIP LOCKED job queue<br/>• Exponential backoff + rate-limit reset<br/>• Poison-job handling (MaxAttempts=5)"]
    end

    subgraph "Storage & Edge Adapters"
        PG[(PostgreSQL 17 + pgvector<br/>• Repositories, Topics, Snapshots<br/>• 24h SHA-256 Analysis Cache<br/>• Enrichment State Machine)]
        GH[GitHub REST API<br/>• WireMock in tests, RateLimiting in prod]
        LLM[OpenAI-Compatible API<br/>• Query & Idea Brainstorming Only<br/>• Deterministic Offline Fallbacks]
    end

    UI <-->|JSON / HTTP 1.1| API
    API --> HybridSearch
    API --> Clusterizer
    API --> ScoringEngine
    API --> PG
    Worker <-->|Poll & Claim| PG
    Worker -->|Enrichment Fetches| GH
    HybridSearch <--> PG
    ScoringEngine <--> PG
    Clusterizer <--> PG
    API -.->|Candidate suggestions only| LLM
```

---

## Algorithmic Design

### 1. Hybrid Search with Reciprocal Rank Fusion ($k=60$)

To retrieve candidate repositories, RepoLens bridges lexical precision and semantic similarity:
- **Full-Text Search:** PostgreSQL `to_tsvector('english', ...)` weights repository name (A), topics (B), description (C), and normalized README content (D).
- **Dense Vector Search:** Exact cosine distance over `pgvector` embeddings (`vector(1536)`).
- **RRF Merge ($k=60$):**

$$\text{RRF}(d) = \sum_{m \in M} \frac{k}{k + r_m(d)}$$

Where $k=60$, $r_m(d)$ is the 1-based rank of repository $d$ in retrieval leg $m \in \{\text{lexical}, \text{vector}\}$. The score is normalized such that the top hit has a score of $1.0$, and exact ties are resolved deterministically by repository ID ascending.

### 2. Connected Components Graph Clustering

Given a candidate set $R$, RepoLens computes undirected similarity edges:

$$\text{Score}(A, B) = \frac{0.5 \cdot \text{Dice}(\text{topics}_A, \text{topics}_B) + 0.3 \cdot \text{Dice}(\text{name}_A, \text{name}_B) + 0.2 \cdot \mathbb{I}(\text{lang}_A = \text{lang}_B)}{\sum \text{applicable weights}}$$

Pairs with $\text{Score}(A, B) \ge 0.30$ form graph edges. RepoLens executes a deterministic Breadth-First Search (BFS) over an ID-ordered adjacency list, guaranteeing:
- Exact reproducibility across candidate list permutations.
- Centrality ordering per cluster: $\text{Centrality}(u) = \frac{1}{|C|-1}\sum_{v \in C, v \ne u} \text{Score}(u, v)$.

### 3. Estimated Novelty (`novelty-v1`)

Novelty is evaluated strictly over the bounded candidate set:

$$\text{Novelty} = \text{round}\left(100 \times \left(0.30 \cdot C_{\text{gap}} + 0.25 \cdot D_{\text{sim}} + 0.20 \cdot S_{\text{cluster}} + 0.15 \cdot A_{\text{act}} + 0.10 \cdot R_{\text{dup}}\right)\right)$$

- $C_{\text{gap}} = 1 - \max \text{Overlap}(\text{idea}, \text{competitors})$
- $D_{\text{sim}} = 1 - \text{MeanPairwiseSimilarity}(\text{candidates})$
- $S_{\text{cluster}} = 1 - \frac{|\text{Largest Cluster}|}{N}$
- $A_{\text{act}} = \text{clamp}\left(1 - \frac{\text{Active}_{\text{90d}}}{20}, 0, 1\right)$
- $R_{\text{dup}} = 1 - \frac{\text{Pairs}(\text{score} \ge 0.70)}{\binom{N}{2}}$

---

## Architectural Trade-offs

Every key architectural decision involves conscious trade-offs between immediate velocity, operational complexity, and long-term scaling limits:

| Decision | Chosen Architecture | Benefit | Downside | Future Breaking Point |
|---|---|---|---|---|
| **1. System Architecture** | **Modular Monolith** (.NET 10) rather than microservices | Single deployable artifact, zero network serialization between feature modules, simple transactional integrity. | Feature modules share compute/memory pools; cannot scale ingestion workers independently from user-facing query APIs. | Team growth requiring independent deployment velocity, or CPU-intensive vector calculations saturating API thread pools. |
| **2. Vector Storage** | **PostgreSQL with `pgvector`** rather than a dedicated vector DB | One persistence layer avoids cross-store synchronization and joins repository metadata to embeddings directly. | Embeddings share PostgreSQL resources; dense similarity currently uses exact cosine search rather than ANN indexing. | Retrieval cost grows with the searchable corpus and should be benchmarked before repository volume is increased substantially; ANN indexing is the first option to evaluate. |
| **3. Hybrid Fusion** | **Reciprocal Rank Fusion (RRF, $k=60$)** rather than score-weighted linear fusion | Eliminates score calibration mismatch between BM25/`ts_rank_cd` and cosine distances; robust against score distribution skews across diverse queries. | Discards raw similarity distance magnitudes (a candidate 10x closer than rank 2 receives only rank-proportional credit). | Search requirements where calibrated relevance probabilities or cross-encoder re-ranking models provide higher precision. |
| **4. Ecosystem Clustering** | **C# graph connected components** rather than a Python clustering sidecar | Keeps deterministic grouping in-process with no second runtime or model dependency. | Pairwise graph construction has quadratic growth; connected components can create chaining bridges at permissive thresholds. | Larger candidate windows would eventually make pairwise construction a bottleneck; sparse nearest-neighbor candidate generation would be a natural optimization to evaluate. |
| **5. Background Jobs** | **PostgreSQL `FOR UPDATE SKIP LOCKED`** rather than an external broker | Avoids another operational dependency while transactions make claims and recovery state durable. | Polling adds database work and completed jobs require retention management. | Sustained workload or multi-region delivery requirements that PostgreSQL queue measurements cannot meet. |
| **6. Scoring Ownership** | **Deterministic versioned code** (`novelty-v1`, `rec-v1`) rather than LLM score prompts | Scores are reproducible, explainable, and directly testable; LLM text cannot override them. | Formula weights need deliberate calibration and do not learn from user feedback automatically. | Evidence that a learned ranking model materially improves relevance while preserving auditability. |

---

## Failure Modes & Resilience

1. **GitHub API Rate Limits:**
   - Detects HTTP 403 with `x-ratelimit-remaining: 0`.
   - Schedules background job retries precisely at `x-ratelimit-reset` epoch rather than blindly thrashing.
2. **Poison Jobs (Permanent Failures):**
   - Repositories deleted or DMCA-takedown (HTTP 404, 410, 451) fail fast with `EnrichmentErrorCategory.Permanent`.
   - Repeated 5xx errors increment attempts and transition to `Failed` once the configured `MaxAttempts = 5` is exhausted.
3. **Interrupted / Crashed Workers:**
   - At worker startup, any jobs stuck in `Processing` for $>15$ minutes are safely recovered back to `Pending` using `Recover()`.
4. **LLM Downtime / Missing API Key:**
   - Both Idea Validation and Recommendation pipelines detect unconfigured or unavailable LLMs and immediately fall back to pre-computed deterministic query plans and candidate banks.

---

## Defensible Engineering Resume / CV Bullets

- **Architected RepoLens**, a .NET 10 and React 19 platform for evidence-backed GitHub ecosystem analysis, using candidate-bounded deterministic scoring while restricting LLMs to query expansion and idea generation.
- **Built hybrid repository retrieval** with PostgreSQL full-text search, pgvector cosine similarity, and Reciprocal Rank Fusion, keeping lexical and semantic search in a single persistence layer.
- **Implemented a PostgreSQL-backed durable enrichment worker** with `FOR UPDATE SKIP LOCKED`, rate-limit-aware retries, crash recovery, and poison-job handling; validated by **93 unit and 71 containerized integration tests**.
