# RepoLens

**GitHub Ecosystem Intelligence** — search GitHub, build your own index of what
you care about, and get evidence-backed answers: ecosystem landscapes, how
saturated a project idea is, portfolio signals, and project recommendations.

Reads only public GitHub data. All scores are deterministic and versioned
(`docs/scoring.md`, `docs/decisions/`); LLMs never compute scores and every LLM
path has a deterministic fallback.

## Flows

| Flow | Path | API |
| --- | --- | --- |
| Explore GitHub | `/discover` | `GET /api/discovery/repositories` |
| Repository detail / enrichment | `/repositories/:id` | `GET/POST /api/repositories/{id}[/refresh]` |
| Search RepoLens (hybrid) | `/search` | `GET /api/search/repositories` |
| Similar projects | detail page | `GET /api/repositories/{id}/similar` |
| Ecosystem analysis | `/ecosystem` | `POST /api/analysis/ecosystem`, `GET /api/analysis/ecosystem/{id}` |
| Validate an Idea | `/validate` | `POST /api/analysis/idea`, `GET /api/analysis/idea/{id}` |
| Analyze Portfolio | `/portfolio` | `GET /api/portfolio/{username}`, `POST /api/portfolio/{username}/marginal` |
| Find My Next Project | `/recommend` | `POST /api/recommendations`, `GET /api/recommendations/{id}` |
| Account & history | `/account` | `/api/auth/*` (optional OAuth) |

## Stack

| Layer | Tech |
| --- | --- |
| Backend | .NET 10 / C# 14, ASP.NET Core minimal APIs, EF Core 10, Npgsql, OpenTelemetry |
| Database | PostgreSQL + pgvector (Docker); tsvector FTS + exact vector search |
| Frontend | React 19, TypeScript, Vite, React Router, TanStack Query, Tailwind v4, shadcn/ui, Zod |
| Testing | xUnit, Testcontainers PostgreSQL, WireMock.Net (GitHub/LLM), GitHub Actions CI |
| Optional external | OpenAI-compatible embeddings/chat endpoints; GitHub OAuth |

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (`global.json`)
- Node.js 24 + npm
- Docker (Docker Desktop or equivalent) — required for the database and
  integration tests, not for building

## Development

```bash
docker compose up -d postgres                     # PostgreSQL + pgvector
dotnet run --project src/RepoLens.Api             # http://localhost:5190/health
cd src/RepoLens.Web && npm install && npm run dev  # http://localhost:5173
```

Configuration lives in `appsettings.json` + environment variables:

| Variable | Purpose | Default |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL | dev values in `appsettings.Development.json` |
| `GitHub__Token` | optional unauthenticated→authenticated rate limits | none |
| `Embedding__Model` / `Embedding__ApiKey` | enable hybrid/similar (OpenAI-compatible `Embedding__BaseUrl`) | off |
| `Llm__Model` / `Llm__ApiKey` | enable LLM-assisted plans/ideas (OpenAI-compatible `Llm__BaseUrl`) | off |
| `Auth__CookieKey` + `GitHub__OAuthClientId`/`GitHub__OAuthClientSecret` + `GitHub__OAuth__CallbackUrl` | GitHub sign-in + history | off |

No real secrets are committed; every optional capability degrades gracefully
(keyword-only search, deterministic fallback plans, anonymous use).

## Tests

```bash
dotnet build RepoLens.sln
dotnet test tests/RepoLens.UnitTests
dotnet test tests/RepoLens.IntegrationTests   # requires Docker (Testcontainers)
cd src/RepoLens.Web && npm run typecheck && npm run build && npm run lint
```

Integration tests never call the real GitHub API — WireMock.Net stands in for
GitHub and the LLM provider, Testcontainers runs real PostgreSQL+pgvector.
CI runs all of the above plus a Docker image build.

## Documentation

- `docs/architecture.md` — layers, flows, cross-cutting decisions
- `docs/product.md` — product language and capability map
- `docs/scoring.md` — novelty / marginal value / recommendation formulas
- `docs/search-ranking.md` — FTS weights, RRF, similarity
- `docs/analysis-limitations.md` — what claims are and are not made
- `docs/decisions/` — ADRs: 001 initial architecture, 002 clustering
  (why not Python/HDBSCAN/ML.NET), 003 search/scoring/LLM boundaries

## Development principles

`AGENTS.md`: modular monolith, feature-oriented growth, single PostgreSQL, no
speculative abstractions (no `GenericRepository<T>`/MediatR/event buses), EF
InMemory never used in tests, deliberate ADR-documented decisions. Known
current limitations (no Docker on a dev box → integration tests run in CI; no
trend/history analysis yet — snapshot history exists but trend features are
deliberately not built).
