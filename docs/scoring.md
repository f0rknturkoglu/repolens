# Scoring

Every RepoLens score is **deterministic, evidence-backed, and versioned**. LLMs
never compute scores — their output is limited to candidate brainstorming and
explanatory text over evidence that was already computed.

## Estimated Novelty (`novelty-v1`)

Input: the analyzed candidate repository set + idea terms (from the idea text).
Output: a score from 0 to 100 and per-component evidence.

| Weight | Component | Formula (0 = saturated, 1 = novel) |
| --- | --- | --- |
| 0.30 | Closest competitor distance | `1 − max overlap(idea terms, competitor terms)` |
| 0.25 | Similarity density | `1 − mean pairwise similarity of candidates` |
| 0.20 | Largest cluster share | `1 − largest cluster size / candidate count` |
| 0.15 | Active competitor scarcity | `1 − min(active(90d) / 20, 1)` |
| 0.10 | Near-duplicate scarcity | `1 − pairs(score ≥ 0.70) / total pairs` |

`score = round(100 * Σ weight·value)`; 100 means the analyzed set showed no
overlap. "Estimated Novelty" is a heuristic over the analyzed candidate set — never an absolute market claim.

### Zero-Candidate & Low-Candidate Semantics

- **$N = 0$ Candidates:** If no candidate repositories are retrieved by the search plan, the engine returns a score of 100 with a single component (`key: "candidates"`: *"No candidate repositories were found in the analyzed set"*). The same API object reports `candidateCount: 0` and `evidenceSufficiency: "insufficient"`. In this state, a high novelty score represents **absence of observed competition** in the query plan, **not proof of uniqueness** across all GitHub. The UI displays the same **Insufficient Evidence** state.
- **$N < 3$ Candidates:** When fewer than 3 candidates are evaluated, the UI flags **Limited Evidence**, indicating that the evaluated sample is small and alternative search keywords may reveal existing solutions.
- **$N \ge 3$ Candidates:** The full 5-component weighted formula (`novelty-v1`) operates with standard candidate coverage.

## Portfolio marginal value (`marginal-v1`)

For a candidate idea against a portfolio's coverage: for each taxonomy category
the idea matches (else the best keyword-matched category), with `r` = current
repositories evidencing that category:

`value = clamp(1 − min(r/3, 1) + (r == 0 ? 0.10 : 0), 0, 1)`

Coverage bands: strong ≥ 3 repos, moderate 1–2, limited 0. Score = mean of the
category values.

## Recommendation score (`rec-v1`)

| Weight | Component | Source |
| --- | --- | --- |
| 0.30 | Originality | Estimated Novelty / 100 |
| 0.20 | Portfolio marginal value | marginal-v1 (0.5 neutral without a profile) |
| 0.20 | Feasibility | scope from text signals (see below) |
| 0.15 | Goal alignment | keyword overlap between goal and category |
| 0.15 | Interest alignment | max keyword overlap over interests |

Feasibility scope: count integration words (api/oauth/webhook/sdk/…) and
infrastructure words (kafka/postgres/kubernetes/llm/…): ≤1 Small (0.9),
≤3 Medium (0.7), ≤5 Large (0.45), more Very Large (0.25); a < 6-week window
subtracts 0.15 from Large/Very Large. Final ranking is `score desc, title asc`;
the top 3 must have distinct categories (diversity). All weights live in code as
versioned constants; changing them changes the formula version, never history.
