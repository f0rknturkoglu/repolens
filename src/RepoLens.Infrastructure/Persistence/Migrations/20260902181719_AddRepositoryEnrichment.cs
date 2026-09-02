using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RepoLens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "enriched_at_utc",
                table: "repositories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "enrichment_jobs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    repository_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_category = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrichment_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "repository_languages",
                columns: table => new
                {
                    repository_id = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    bytes = table.Column<long>(type: "bigint", nullable: false),
                    captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repository_languages", x => new { x.repository_id, x.language });
                });

            migrationBuilder.CreateTable(
                name: "repository_readmes",
                columns: table => new
                {
                    repository_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    raw_content = table.Column<string>(type: "text", nullable: false),
                    text_content = table.Column<string>(type: "text", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fetched_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repository_readmes", x => x.repository_id);
                });

            migrationBuilder.CreateTable(
                name: "repository_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    repository_id = table.Column<long>(type: "bigint", nullable: false),
                    stars = table.Column<int>(type: "integer", nullable: false),
                    forks = table.Column<int>(type: "integer", nullable: false),
                    open_issues = table.Column<int>(type: "integer", nullable: false),
                    captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repository_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "repository_topics",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    repository_id = table.Column<long>(type: "bigint", nullable: false),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    added_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repository_topics", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_enrichment_jobs_repository_id_status",
                table: "enrichment_jobs",
                columns: new[] { "repository_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_enrichment_jobs_status",
                table: "enrichment_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_repository_readmes_content_hash",
                table: "repository_readmes",
                column: "content_hash");

            migrationBuilder.CreateIndex(
                name: "IX_repository_snapshots_repository_id_captured_at_utc",
                table: "repository_snapshots",
                columns: new[] { "repository_id", "captured_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_repository_topics_repository_id_topic",
                table: "repository_topics",
                columns: new[] { "repository_id", "topic" },
                unique: true);

            // Database-level guarantee: never more than one active job per repository.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_enrichment_jobs_one_active_per_repository ON enrichment_jobs (repository_id) WHERE status IN (0, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_enrichment_jobs_one_active_per_repository;");
            migrationBuilder.DropTable(
                name: "enrichment_jobs");

            migrationBuilder.DropTable(
                name: "repository_languages");

            migrationBuilder.DropTable(
                name: "repository_readmes");

            migrationBuilder.DropTable(
                name: "repository_snapshots");

            migrationBuilder.DropTable(
                name: "repository_topics");

            migrationBuilder.DropColumn(
                name: "enriched_at_utc",
                table: "repositories");
        }
    }
}
