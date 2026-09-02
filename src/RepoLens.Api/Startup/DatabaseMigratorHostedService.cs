using Microsoft.EntityFrameworkCore;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Api.Startup;

/// <summary>
/// Applies pending EF Core migrations when the API starts. Idempotent (PostgreSQL
/// tracks applied migrations), so every instance of the single-instance monolith
/// converges on the same schema. Runs in tests too — WebApplicationFactory boots
/// this hosted service, which is how integration tests get their schema.
/// </summary>
public sealed class DatabaseMigratorHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RepoLensDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
