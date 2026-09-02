# ADR 001: Initial architecture — modular monolith + PostgreSQL + pgvector

Status: Accepted

## Context

RepoLens needs to analyze the GitHub ecosystem (similarity, clustering,
novelty, portfolio gaps). At this stage there is no real product code, no
measured load, and no proven query patterns. The goal is a clean walking
skeleton that supports feature development with tests and CI, while leaving
room for future extraction.

## Decision

Build RepoLens as a **modular monolith**:

- one ASP.NET Core API deployable, organized internally by feature,
- one PostgreSQL database with the **pgvector** extension for vector
  similarity,
- a React SPA,
- integration tests against real PostgreSQL via Testcontainers.

## Consequences

- One database and one deployable keep operations, migrations, and local
  development simple.
- Feature modules inside the monolith preserve clear boundaries, so a future
  extraction is possible without a rewrite.
- pgvector keeps similarity queries next to the relational data they depend
  on; no separate vector database to operate, sync, or back up.
- Background workers and a GitHub adapter can be added to the same
  deployable, or extracted, as needs become concrete.

## Alternatives considered

### Microservices

- **Rejected because:** the starting scale does not justify distributed
  deployment, service discovery, inter-service contracts, and per-service
  CI/ops overhead. Premature distribution would slow down the first features.

### Dedicated search / vector infrastructure

(e.g. OpenSearch/Elasticsearch or a standalone vector database)

- **Rejected because:** operational complexity (extra services, sync jobs,
  backup concerns) without evidence of a need. PostgreSQL + pgvector covers
  the anticipated similarity workloads; if vector scale or capabilities later
  exceed PostgreSQL, that part of the system can be extracted while the rest
  of the monolith stays.

## Revisit when

- real workloads show PostgreSQL/pgvector limits (scale, query features,
  availability), or
- a second deployable with genuinely independent scaling requirements
  emerges.
