# RepoLens API Surface

Derived from `src/RepoLens.Api/Endpoints/*.cs`. All routes are relative to the
API base (dev: `http://localhost:5190`). Errors use ProblemDetails with stable
`code` values (`github_rate_limited`, `invalid_query`, `not_found`, …); raw
GitHub error text is never forwarded.

## Health / Operational

| Method | Route | Purpose | Auth | Frontend consumer |
| --- | --- | --- | --- | --- |
| GET | `/health` | API + PostgreSQL reachability | — | home footer badge |

## Discovery (GitHub search → local persistence)

| Method | Route | Purpose | Auth | Frontend consumer |
| --- | --- | --- | --- | --- |
| GET | `/api/discovery/repositories?q&page&perPage` | GitHub repository search, normalized + upserted (identity = GitHub id); auto-enqueues enrichment for never-enriched repos. Returns query, pagination, rate-limit subset, items. | — | `/discover` |
| Errors | `github_rate_limited` 429, `invalid_query` 400, `upstream_error/unavailable/malformed` 502/503 | | | |

## Repositories / Enrichment

| Method | Route | Purpose | Auth | Frontend consumer |
| --- | --- | --- | --- | --- |
| GET | `/api/repositories/{id}` | Enriched detail: metadata, topics, language breakdown, README text, enrichment status. | — | `/repositories/:id` |
| POST | `/api/repositories/{id}/refresh` | Manual enrichment enqueue (spam-guarded; 409 active job, 429 too recent). Rate limit: `expensive`. | — | detail page button |
| Errors | 404 `not_found` | | | |

## Internal Search & Similarity

| Method | Route | Purpose | Auth | Frontend consumer |
| --- | --- | --- | --- | --- |
| GET | `/api/search/repositories?q&language&archived&minStars&page&perPage` | FTS + optional vector legs merged with RRF; `method: Keyword|Hybrid`; explicit `vectorFallbackReason` when embeddings are unconfigured/unavailable. | — | `/search` |
| GET | `/api/repositories/{id}/similar` | Nearest neighbors via stored embeddings; self excluded; per-item similarity + shared-topic/language evidence; `status: not_indexed|unavailable` explain empty lists. | — | detail page “Similar Projects” |

## Ecosystem Analysis

| Method | Route | Purpose | Auth | Frontend consumer |
| --- | --- | --- | --- | --- |
| POST | `/api/analysis/ecosystem` | Deterministic query variants → bounded candidates → similarity-graph clusters → metrics + evidence snapshot (persisted). Rate limit: `expensive`. | — | `/ecosystem` |
| GET | `/api/analysis/ecosystem/{id}` | Replay stored snapshot (metrics/clusters/evidence). | — | `/ecosystem?analysis=id` |
| Errors | 400 invalid query, 429 `github_rate_limited` | | | |

## Idea Validation

| Method | Route | Purpose | Auth | Frontend consumer |
| --- | --- | --- | --- | --- |
| POST | `/api/analysis/idea` | Bounded search plan (LLM-assisted with deterministic fallback) → candidates → novelty-v1 score + components, competitors, gaps, limitations; 24h cache keyed by normalized idea. Returns `servedFromCache`, the actual `candidateCount`, and `evidenceSufficiency` (`insufficient`, `limited`, or `sufficient`) so a score of 100 with no candidates cannot be mistaken for verified uniqueness. Rate limit: `expensive`. | — | `/validate` |
| GET | `/api/analysis/idea/{id}` | Replay a stored validation. | — | `/validate?id=…` (history) |

## Portfolio Intelligence

| Method | Route | Purpose | Auth | Frontend consumer |
| --- | --- | --- | --- | --- |
| GET | `/api/portfolio/{username}` | Public profile → taxonomy-v1 signals + coverage bands + evidence; 30 min replay cache. | — | `/portfolio` |
| POST | `/api/portfolio/{username}/marginal` | marginal-v1 value of a candidate idea against the profile’s current coverage. Rate limit: `expensive`. | — | portfolio page panel |

## Recommendations

| Method | Route | Purpose | Auth | Frontend consumer |
| --- | --- | --- | --- | --- |
| POST | `/api/recommendations` | LLM/fallback candidates → per-candidate GitHub validation → rec-v1 ranking + feasibility + portfolio marginal value + diversity + `whyRankedHere`; 24h request-hash cache. Rate limit: `expensive`. | — | `/recommend` |
| GET | `/api/recommendations/{id}` | Replay a stored recommendation report. | — | `/recommend?id=…` (history) |

## Authentication & History (optional; feature off until configured)

| Method | Route | Purpose | Auth | Frontend consumer |
| --- | --- | --- | --- | --- |
| GET | `/api/auth/status` | OAuth enabled + current user (nullable). | — | account page |
| GET | `/api/auth/login` | Redirect to GitHub OAuth (404 when unconfigured). | — | sign-in button |
| GET | `/api/auth/callback` | OAuth callback → upsert user → signed session cookie. | — | (browser redirect) |
| POST | `/api/auth/logout` | Clears session cookie. | session | account page |
| GET | `/api/auth/me` | Current user; 401 anonymous. | session | account page |
| GET | `/api/auth/me/history` | Saved analyses list (kind, reference id, title, status, version); 401 anonymous. | session | account page |

When `Auth:CookieKey` (and OAuth client credentials) are unset the feature is
off: `/api/auth/status` reports `enabled:false`, `/login` returns 404, and
signed-in-only routes return 401 — the UI renders the disabled state instead of
a misleading login flow.

## Rate limiting

Global 300 req/min per client; `expensive` policy (20 req/min) on analysis
POSTs, repository refresh, and recommendations. 429s include ProblemDetails;
the UI maps them to actionable messages.
