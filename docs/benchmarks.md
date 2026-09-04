# Reference Benchmark Fixtures

These six fixture-backed scenarios document the expected behavior of RepoLens' deterministic scoring and clustering algorithms across distinct software-ecosystem shapes. They are reference cases for regression, edge-case validation, and explaining algorithm behavior; they are not production load tests or live GitHub benchmarks.

> **Classification notice:** The repository names and metadata below belong to fixed reference inputs calibrated on **2026-09-04**. Live GitHub stars, activity, topics, and search results can change, so a new live analysis is not expected to reproduce the fixture values.

---

## Methodology

- **Fixture source:** Fixed candidate attributes calibrated from representative GitHub search snapshots on **2026-09-04**.
- **Reproduction:** Re-evaluating the same captured inputs with the same formula version produces deterministic scores and ordering. Live GitHub queries are intentionally outside that guarantee.
- **Scoring boundary:** Numeric values come only from versioned deterministic formulas (`novelty-v1`, `rec-v1`); LLM-generated text does not set scores or weights.
- **Purpose:** The fixtures exercise saturated, niche, close-match, sparse, broad/noisy, and multi-language candidate sets.
- **Performance scope:** No latency or capacity conclusion is drawn from these fixtures. Any local timing must report sample count, hardware/runtime, cold-versus-warm conditions, median, and p95 before it is presented as a benchmark; local results are not production SLAs.

---

## Algorithmic Reference Scenarios

| # | Archetype | Fixture Candidate Count ($N$) | Top Match Overlap | Expected Novelty (`novelty-v1`) |
|---|---|---|---|---|
| **1** | **Saturated Space** | 42 repos | 0.88 (shadcn-ui/ui) | **25 / 100** |
| **2** | **Niche Space** | 18 repos | 0.42 (stripe/pg-schema-diff) | **68 / 100** |
| **3** | **Closely Matched** | 35 repos | 0.76 (quickwit-oss/tantivy) | **28 / 100** |
| **4** | **Highly Novel** | 8 repos | 0.18 (basetenlabs/audiocraft) | **91 / 100** |
| **5** | **Noisy / Broad** | 50 repos | 0.35 (rclone/rclone) | **41 / 100** |
| **6** | **Multi-Language** | 29 repos | 0.54 (iden3/circom) | **56 / 100** |

---

## Detailed Benchmark Cases

### Case 1: Saturated Space (High Redundancy, Low Novelty)

- **Input Idea:** `"React component UI library styled with Tailwind CSS and Radix primitives"`
- **Target Category:** Frontend / Developer Tooling
- **Search Plan:**
  - Query 1 (exact): `"react component library tailwind"` (source: `fallback`)
  - Query 2 (topic): `topic:react topic:tailwind-css` (source: `fallback`)
  - Query 3 (semantic/expanded): `"headless ui components accessible"` (source: `llm`)
- **Candidate Set Size:** 42 repositories evaluated.
- **Top Competitors Discovered:**
  1. `shadcn-ui/ui` (Stars: 82.4k, Overlap: 0.88, Shared: `react`, `tailwind`, `components`)
  2. `tailwindlabs/headlessui` (Stars: 24.1k, Overlap: 0.74, Shared: `react`, `tailwind`)
  3. `nextui-org/nextui` (Stars: 21.8k, Overlap: 0.69, Shared: `react`, `components`)
- **Deterministic Component Breakdown (`novelty-v1`):**
  - Closest Competitor Distance (Weight 30%): `1 - 0.88 = 0.12` → **3.6 pts**
  - Similarity Density (Weight 25%): Mean pairwise similarity `0.64` → `1 - 0.64 = 0.36` → **9.0 pts**
  - Largest Cluster Share (Weight 20%): Dominant cluster holds 71% of candidates → `1 - 0.71 = 0.29` → **5.8 pts**
  - Active Competitor Scarcity (Weight 15%): 31 active within 90 days → `clamp(1 - 31/20, 0, 1) = 0.00` → **0.0 pts**
  - Near-Duplicate Scarcity (Weight 10%): 38% of pairs have similarity $\ge 0.70$ → `1 - 0.38 = 0.62` → **6.2 pts**
- **Calculated Novelty Score:** `round(100 * (0.036 + 0.090 + 0.058 + 0.000 + 0.062)) =` **25 / 100** *(Crowded space)*.
- **Expected Behavior:** The score reflects the fixture's high overlap, active competition, and dominant cluster without making a claim beyond the captured candidate set.

---

### Case 2: Niche Space (Moderate Exploration, High Opportunity)

- **Input Idea:** `"PostgreSQL schema-migration load-testing laboratory that replays real workload DDL"`
- **Target Category:** Databases / Testing
- **Search Plan:**
  - Query 1: `"postgres migration load testing"` (source: `fallback`)
  - Query 2: `topic:postgres topic:database-migrations` (source: `fallback`)
  - Query 3: `"ddl workload replay simulation postgresql"` (source: `llm`)
- **Candidate Set Size:** 18 repositories evaluated.
- **Top Competitors Discovered:**
  1. `stripe/pg-schema-diff` (Stars: 2.1k, Overlap: 0.42, Shared: `postgres`, `migration`)
  2. `xataio/pgroll` (Stars: 3.4k, Overlap: 0.38, Shared: `postgres`, `migration`)
  3. `ankane/strong_migrations` (Stars: 4.8k, Overlap: 0.31, Shared: `postgres`, `migration`)
- **Deterministic Component Breakdown (`novelty-v1`):**
  - Closest Competitor Distance (Weight 30%): `1 - 0.42 = 0.58` → **17.4 pts**
  - Similarity Density (Weight 25%): Mean pairwise similarity `0.28` → `1 - 0.28 = 0.72` → **18.0 pts**
  - Largest Cluster Share (Weight 20%): Largest cluster holds 33% of candidates → `1 - 0.33 = 0.67` → **13.4 pts**
  - Active Competitor Scarcity (Weight 15%): 6 active competitors → `1 - 6/20 = 0.70` → **10.5 pts**
  - Near-Duplicate Scarcity (Weight 10%): Only 8% near-duplicate pairs → `1 - 0.08 = 0.92` → **9.2 pts**
- **Calculated Novelty Score:** `round(100 * (0.174 + 0.180 + 0.134 + 0.105 + 0.092)) =` **68 / 100** *(Mostly open space)*.
- **Expected Behavior:** The fixture contains schema-diff and migration-linting tools but less evidence for load testing and traffic replay during DDL execution, producing a candidate-scoped gap hypothesis.

---

### Case 3: Closely Matched Project (High Feature Overlap)

- **Input Idea:** `"Fast embedded full-text search engine library written in Rust with BM25 ranking"`
- **Target Category:** Databases / Developer Tooling
- **Search Plan:**
  - Query 1: `"rust embedded full text search engine"` (source: `fallback`)
  - Query 2: `topic:rust topic:search-engine` (source: `fallback`)
- **Candidate Set Size:** 35 repositories evaluated.
- **Top Competitors Discovered:**
  1. `quickwit-oss/tantivy` (Stars: 12.8k, Overlap: 0.76, Shared: `rust`, `search`, `bm25`, `embedded`)
  2. `meilisearch/meilisearch` (Stars: 46.2k, Overlap: 0.62, Shared: `rust`, `search`)
  3. `valeriansaliou/sonic` (Stars: 19.5k, Overlap: 0.51, Shared: `rust`, `search`)
- **Calculated Novelty Score:** **28 / 100** *(Partially explored / crowded)*.
- **Expected Behavior:** Tantivy is surfaced as the closest fixture match, with overlap reasons citing shared tokens such as `bm25`, `search`, and `embedded`.

---

### Case 4: Highly Novel Concept (Sparse Landscape, High Novelty)

- **Input Idea:** `"CRDT-based collaborative multi-track audio workstation running over WebRTC data channels in WebAssembly"`
- **Target Category:** Web / Audio / Distributed Systems
- **Search Plan:**
  - Query 1: `"crdt collaborative audio workstation webrtc"` (source: `fallback`)
  - Query 2: `topic:crdt topic:webrtc topic:audio` (source: `fallback`)
- **Candidate Set Size:** 8 repositories evaluated.
- **Top Competitors Discovered:**
  1. `yjs/yjs` (Stars: 17.1k, Overlap: 0.22, Shared: `crdt`, `collaborative`)
  2. `zeno-audio/zeno` (Stars: 420, Overlap: 0.18, Shared: `audio`, `web`)
  3. `feross/simple-peer` (Stars: 7.2k, Overlap: 0.14, Shared: `webrtc`)
- **Calculated Novelty Score:** **91 / 100** *(Mostly open space)*.
- **Candidate Set Boundary Note:** "No overlapping repositories were found in the analyzed candidate set matching the complete multi-track audio CRDT concept (8 signal points evaluated via search plan)."
- **Expected Behavior:** The fixture contains component technologies in isolation but little evidence for their combined use; the high score remains bounded to those eight candidates.

---

### Case 5: Noisy / Broad Query (High Variance, Low Concentration)

- **Input Idea:** `"Cloud sync tool"`
- **Target Category:** Developer Tooling / DevOps
- **Search Plan:**
  - Query 1: `"cloud sync tool"` (source: `fallback`)
- **Candidate Set Size:** 50 repositories evaluated (GitHub API page limit).
- **Top Competitors Discovered:**
  1. `rclone/rclone` (Stars: 47.3k, Overlap: 0.35)
  2. `syncthing/syncthing` (Stars: 64.2k, Overlap: 0.32)
  3. `owncloud/core` (Stars: 8.9k, Overlap: 0.28)
- **Calculated Novelty Score:** **41 / 100** *(Broad / partially explored)*.
- **Expected Behavior:** Low similarity density and nine disconnected fixture clusters expose the ambiguity of a broad query.

---

### Case 6: Multi-Language Ecosystem (Decentralized Architectures)

- **Input Idea:** `"Zero-knowledge proof circuit compiler and verifier with constraint optimization"`
- **Target Category:** Cryptography / Compilers
- **Search Plan:**
  - Query 1: `"zk proof circuit compiler constraint optimization"` (source: `fallback`)
  - Query 2: `topic:zero-knowledge-proofs topic:circuit-compiler` (source: `fallback`)
- **Candidate Set Size:** 29 repositories evaluated.
- **Language Breakdown:** Rust (48%), C++ (24%), Go (17%), TypeScript (11%).
- **Top Competitors Discovered:**
  1. `iden3/circom` (Stars: 2.1k, Overlap: 0.54, Language: C++)
  2. `arkworks-rs/snark` (Stars: 1.4k, Overlap: 0.49, Language: Rust)
  3. `consensys/gnark` (Stars: 2.8k, Overlap: 0.46, Language: Go)
- **Calculated Novelty Score:** **56 / 100** *(Partially explored space)*.
- **Expected Behavior:** The clusterizer segments the fixed candidates into language-oriented connected components with deterministic centrality ordering.

---

## Reference Invariants

1. **Saturation sensitivity:** Near-duplicate and densely connected fixtures score lower than sparse fixtures.
2. **Determinism:** Static inputs and a fixed formula version produce the same score and ordering.
3. **Boundedness:** Scores remain in $[0, 100]$ for `novelty-v1` and $[0, 1]$ for `rec-v1`.
4. **Replay behavior:** Integration tests verify that identical normalized requests reuse the stored PostgreSQL snapshot without another GitHub search; no latency SLA is inferred.
