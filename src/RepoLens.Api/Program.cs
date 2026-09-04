using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.RateLimiting;
using RepoLens.Api.Errors;
using RepoLens.Application.Observability;
using RepoLens.Api.Endpoints;
using RepoLens.Api.Identity;
using RepoLens.Api.Startup;
using RepoLens.Api.Workers;
using RepoLens.Application.Identity;
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
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// --- Optional GitHub OAuth + signed session cookies (feature off until Auth:CookieKey set). ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(builder.Configuration
    .GetSection(AuthCookieSettings.SectionName)
    .Get<AuthCookieSettings>() ?? new AuthCookieSettings());
builder.Services.AddSingleton(builder.Configuration
    .GetSection(GitHubOAuthSettings.SectionName)
    .Get<GitHubOAuthSettings>() ?? new GitHubOAuthSettings());
builder.Services.AddScoped<AuthSessionService>();

// --- Rate limiting: cheap global default; "expensive" policy protects costly
// analysis/refresh endpoints (6/min per client). ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
        context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(context),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 300,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddFixedWindowLimiter("expensive", policy =>
    {
        policy.PermitLimit = 20;
        policy.Window = TimeSpan.FromMinutes(1);
        policy.QueueLimit = 0;
    });
});

// --- OpenTelemetry (export opt-in via OTEL_EXPORTER_OTLP_ENDPOINT). ---
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("RepoLens.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(RepoLensMetrics.MeterName));

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
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapDiscoveryEndpoints();
app.MapRepositoryEndpoints();
app.MapSearchEndpoints();
app.MapAnalysisEndpoints();
app.MapIdeaValidationEndpoints();
app.MapPortfolioEndpoints();
app.MapRecommendationEndpoints();
app.MapAuthEndpoints();

app.Run();

static string ClientKey(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString()
    ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
    ?? "anonymous";

/// <summary>Entry-point type; public so integration tests can host the app via WebApplicationFactory.</summary>
public partial class Program;
