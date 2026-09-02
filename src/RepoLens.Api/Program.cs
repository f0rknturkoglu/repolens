using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RepoLens.Api.Errors;
using RepoLens.Api.Endpoints;
using RepoLens.Api.Startup;
using RepoLens.Api.Workers;
using RepoLens.Application;
using RepoLens.Application.Enrichment;
using RepoLens.Infrastructure;
using RepoLens.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- Logging: human-readable console in Development, structured JSON elsewhere. ---
builder.Logging.ClearProviders();

if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
}
else
{
    builder.Logging.AddJsonConsole(options =>
    {
        options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
        options.UseUtcTimestamp = true;
    });
}

// --- Database: single PostgreSQL instance. ---
// Resolution order: environment variable ConnectionStrings__DefaultConnection,
// then appsettings.json / appsettings.{Environment}.json. Failing fast here beats
// failing at the first request with an opaque Npgsql error.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. "
        + "Set ConnectionStrings__DefaultConnection or add it to appsettings.{Environment}.json.");

var enrichmentSettings = builder.Configuration
    .GetSection(EnrichmentSettings.SectionName)
    .Get<EnrichmentSettings>() ?? new EnrichmentSettings();

builder.Services.AddApplication(enrichmentSettings);
builder.Services.AddInfrastructure(connectionString);

// --- Health checks: /health reports 200 only when the API can reach PostgreSQL. ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RepoLensDbContext>("database");

// --- Typed upstream-error mapping (GitHub rate limits, 5xx, network…). ---
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// --- OpenTelemetry: ASP.NET Core and HttpClient instrumentation. ---
// Export is opt-in: set OTEL_EXPORTER_OTLP_ENDPOINT to send traces/metrics to a
// collector (e.g. a local OTLP endpoint). Nothing is exported until then.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("RepoLens.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
}

// --- Schema: apply EF migrations at startup (idempotent). ---
builder.Services.AddHostedService<DatabaseMigratorHostedService>();

// --- Background enrichment worker (durable PostgreSQL-backed jobs). ---
builder.Services.AddHostedService<EnrichmentWorker>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapHealthChecks("/health");
app.MapDiscoveryEndpoints();
app.MapRepositoryEndpoints();
app.MapSearchEndpoints();

app.Run();

/// <summary>Entry-point type; public so integration tests can host the app via WebApplicationFactory.</summary>
public partial class Program;
