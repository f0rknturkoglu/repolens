# Analysis limitations

These limits apply to every ecosystem/idea/portfolio/recommendation report and
are shown to users; claims never exceed them.

## Candidate scope

- Candidates come from a bounded set of GitHub searches run at analysis time
  (≤ ~90 repositories per ecosystem analysis, one query per recommendation
  candidate). The report describes **that set**, never all of GitHub.
- Search is text-based; repositories whose metadata (description/topics/README)
  does not describe their function are underrepresented.
- "Observed gaps" are hypotheses scoped to the analyzed candidate set. RepoLens
  does not say "this does not exist on GitHub"; it says "less represented in
  the analyzed candidate set".
- No historical data exists yet: age/activity distributions describe the
  current snapshot. "Trends" are only ever reported when snapshot history
  supports them.

## Similarity signals

- Similarity uses topics, name tokens, and primary language only. Two
  repositories implementing the same thing in different languages with
  different names/topics can score 0; two repos sharing only a generic topic
  can be over-grouped.
- Enrichment quality matters: repositories with no README/topics contribute
  less signal. Similarity thresholds (0.30, 0.70) are fixed constants tuned by
  judgement, not by measurement on a broad dataset.

## Embeddings and vectors

- Vector search exists only when an embedding provider is configured; model and
  dimensions are configuration. Changing the model invalidates stored vectors
  until the backfill re-embeds (bounded per worker cycle).
- Similarity display (`sim = 1 − cosine_distance/2`) is a monotonic transform
  of cosine similarity — precision beyond one or two decimals is meaningless
  and the UI never shows fake precision.

## LLM boundaries

- LLM output is restricted to: search-plan query suggestions, candidate idea
  brainstorming, and (future) explanations over existing evidence.
- LLMs never compute novelty, similarity, feasibility, portfolio signals, or
  ranking; unparsable/unavailable LLM output always falls back to deterministic
  behavior (tested).
- Gap hypotheses and differentiation statements are deterministic and scoped as
  above; no LLM invents competitors or claims absence.

## Scoring

- "Estimated Novelty" is a heuristic with a versioned formula
  (`novelty-v1`, docs/scoring.md) — an estimate from observed candidates, not a
  scientific or market measure.
- "Portfolio signals" are evidence counts from public metadata. RepoLens never
  grades skills and never concludes "you do not know X" — limited evidence is
  reported as limited evidence.
- Recommendation feasibility is a relative scope estimate from description
  signals (Small…Very Large), never a schedule promise.

## Rate limits

- Unauthenticated GitHub API use has low rate limits; analyses may hit
  `429 rate_limited` and succeed on retry after the reset. Serving a token
  (`GitHub__Token`) or OAuth is recommended for heavy use.
