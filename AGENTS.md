# AGENTS.md

Guidelines for AI agents (and humans) working in this repository.

## Architecture

- **Modular monolith.** One deployable ASP.NET Core API plus a React SPA. No microservices.
- **Feature-oriented.** Application code will grow as feature modules (`Discovery`, `Analysis`, `Recommendation`, `Portfolio`); keep related code together per feature.
- **Single production database**: PostgreSQL with the `pgvector` extension, accessed through EF Core (`RepoLens.Infrastructure.Persistence.RepoLensDbContext`).
- External systems (GitHub API, etc.) are accessed through adapters at the edge, never from domain logic.
- No speculative abstractions: no `GenericRepository<T>`, no `BaseService<T>`, no unnecessary interfaces, no CQRS/MediatR/event-bus boilerplate.
- Business logic must not depend on EF Core — the `Application`/`Domain` layers stay persistence-agnostic.

## Development

Before a task is done:

1. Build the affected .NET projects (`dotnet build` on the solution or project).
2. Run the affected unit tests.
3. Run the affected integration tests (they need Docker; see `tests/RepoLens.IntegrationTests`).
4. If the frontend changed, run `npm run typecheck` and `npm run build` (and `npm run lint`).
5. Leave no new compiler or linter errors or warnings.

Integration tests cannot run without a Docker daemon. If Docker is unavailable on a machine, state that clearly instead of skipping tests silently; the CI workflow always runs them.

## Testing

- Pure logic → unit tests in `tests/RepoLens.UnitTests`.
- Database and external-system behavior → integration tests in `tests/RepoLens.IntegrationTests`.
- **Never** use EF Core InMemory provider for database tests.
- PostgreSQL integration tests use Testcontainers (`pgvector/pgvector:pg17` image via `PostgresContainerFixture`).
- External HTTP APIs are mocked with WireMock.Net.
- No fake entities/services: tests exercise real code paths (integration tests host the real API through `WebApplicationFactory`).

## Scope control

Do not:

- add dependencies that are not needed for the current task;
- create microservices or new deployables;
- add generic repository/service abstractions;
- add infrastructure "because we might need it later";
- expand beyond the task's scope.

## Agent behavior

If a change requires an architectural decision, do not apply it silently. Before applying:

- state the problem,
- list the options considered,
- write down the trade-offs.

Large architectural decisions are made deliberately and documented in `docs/decisions/` (ADRs) — changes to them need the same treatment.
