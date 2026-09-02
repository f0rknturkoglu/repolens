using Microsoft.EntityFrameworkCore;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>
/// RepoLens relational store (PostgreSQL + pgvector).
///
/// Domain entities and their EF Core mappings are added here as features land
/// (Discovery, Analysis, Recommendation, Portfolio). Until then the context
/// intentionally has no DbSets — it exists to prove database connectivity.
/// </summary>
public sealed class RepoLensDbContext(DbContextOptions<RepoLensDbContext> options) : DbContext(options);
