using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RepoLens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEcosystemAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ecosystem_analyses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    query = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    metrics_json = table.Column<string>(type: "jsonb", nullable: true),
                    variants_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecosystem_analyses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ecosystem_analysis_candidates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    analysis_id = table.Column<long>(type: "bigint", nullable: false),
                    repository_id = table.Column<long>(type: "bigint", nullable: false),
                    query_variant = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rank_in_variant = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecosystem_analysis_candidates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ecosystem_clusters",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    analysis_id = table.Column<long>(type: "bigint", nullable: false),
                    label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    members_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecosystem_clusters", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ecosystem_analyses_query_created_at_utc",
                table: "ecosystem_analyses",
                columns: new[] { "query", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ecosystem_analysis_candidates_analysis_id_repository_id",
                table: "ecosystem_analysis_candidates",
                columns: new[] { "analysis_id", "repository_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ecosystem_analysis_candidates_repository_id",
                table: "ecosystem_analysis_candidates",
                column: "repository_id");

            migrationBuilder.CreateIndex(
                name: "IX_ecosystem_clusters_analysis_id",
                table: "ecosystem_clusters",
                column: "analysis_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ecosystem_analyses");

            migrationBuilder.DropTable(
                name: "ecosystem_analysis_candidates");

            migrationBuilder.DropTable(
                name: "ecosystem_clusters");
        }
    }
}
