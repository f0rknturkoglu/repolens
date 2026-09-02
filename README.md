# RepoLens

**GitHub Ecosystem Intelligence** — a developer intelligence platform that finds
similar projects on GitHub, analyzes repository clusters, and evaluates how
saturated (or original) a project idea is. Planned future capabilities include
portfolio gap analysis and personalized project recommendations.

## Current status

**Walking skeleton / infrastructure only.** No product features are implemented
yet. The repository proves the development chain
**browser → React → ASP.NET Core → PostgreSQL + pgvector**:

- `GET /health` on the API returns `200 Healthy` only when the API can reach
  PostgreSQL (the endpoint includes a database health check).
- The SPA polls `/health` via TanStack Query and shows **API Status: Healthy /
  Unavailable**.
- Integration tests run against a real PostgreSQL container (Testcontainers,
  `pgvector/pgvector:pg17` image) and verify the pgvector extension works.

## Stack

| Layer | Tech |
| --- | --- |
| Backend | .NET 10 / C# 14, ASP.NET Core Web API, EF Core 10, Npgsql |
| Database | PostgreSQL + pgvector (Docker) |
| Frontend | React 19, TypeScript, Vite, React Router, TanStack Query, Tailwind CSS v4, shadcn/ui, Zod |
| Testing | xUnit, Testcontainers for .NET, WireMock.Net |
| Infra | Docker Compose, GitHub Actions CI |
| Observability | Structured logging (JSON console), OpenTelemetry (ASP.NET Core + HttpClient instrumentation) |

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (see `global.json`)
- Node.js 24 + npm
- Docker (Docker Desktop or equivalent) — required for the database and for the
  integration tests; not required to *build* the code

## Project structure

```
repolens/
├── src/
│   ├── RepoLens.Api/            # ASP.NET Core API (composition root, /health)
│   ├── RepoLens.Application/    # future feature modules (Discovery, Analysis, …) — empty
│   ├── RepoLens.Domain/         # future domain entities — empty
│   ├── RepoLens.Infrastructure/ # EF Core DbContext + DI wiring
│   └── RepoLens.Web/            # React + TypeScript + Vite SPA
├── tests/
│   ├── RepoLens.UnitTests/      # pure-logic unit tests (empty: no domain logic yet)
│   └── RepoLens.IntegrationTests/# Testcontainers PostgreSQL + pgvector tests
├── docs/
│   ├── product.md
│   ├── architecture.md
│   └── decisions/               # ADRs
├── .github/workflows/ci.yml
├── AGENTS.md
├── docker-compose.yml
├── .editorconfig
├── global.json
└── RepoLens.sln
```

## Development

Two supported workflows:

### 1. Database in Docker, apps on the host (recommended for daily work)

```bash
# Start PostgreSQL + pgvector (development-only credentials, port 5432)
docker compose up -d postgres
```

Backend (from the repository root):

```bash
dotnet run --project src/RepoLens.Api
# → http://localhost:5190/health
```

Frontend (in `src/RepoLens.Web`):

```bash
npm install
npm run dev
# → http://localhost:5173  (Vite proxies /health to the API on :5190)
```

The browser should show **API Status: Healthy**.

### 2. Full stack in Docker

```bash
docker compose --profile full up --build
# → http://localhost:8080 (nginx serves the SPA and proxies /health and /api to the API)
```

### Configuration

Development defaults live in `src/RepoLens.Api/appsettings.Development.json`
(database `repolens`, user `repolens`, password `repolens_dev_password` on
`localhost:5432`). **These are development-only values** — override anything via
environment variables, e.g.:

```bash
ConnectionStrings__DefaultConnection="Host=…;Database=…;Username=…;Password=…"
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317   # opt-in OpenTelemetry export
```

No real secrets are committed.

## Tests

```bash
# Backend: build + tests
dotnet build RepoLens.sln
dotnet test tests/RepoLens.UnitTests
dotnet test tests/RepoLens.IntegrationTests   # requires Docker; starts a pgvector container

# Frontend
cd src/RepoLens.Web
npm run typecheck
npm run build
npm run lint
```

Testing rules:

- pure logic → unit tests; database/external behavior → integration tests;
- EF Core InMemory is never used — integration tests run on real PostgreSQL via
  Testcontainers;
- external HTTP APIs (future GitHub adapter) are mocked with WireMock.Net.

## Development principles

See `AGENTS.md` for the full rules. In short: modular monolith, feature-oriented
growth, single PostgreSQL database, no speculative abstractions
(`GenericRepository<T>`, CQRS/MediatR, event buses, extra infrastructure), and
deliberate ADR-documented architectural decisions.
