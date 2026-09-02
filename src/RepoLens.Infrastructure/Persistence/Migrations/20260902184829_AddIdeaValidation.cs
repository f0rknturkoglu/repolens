using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RepoLens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdeaValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idea_validations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    idea_text = table.Column<string>(type: "text", nullable: false),
                    idea_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    novelty_formula = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    search_plan_json = table.Column<string>(type: "jsonb", nullable: true),
                    metrics_json = table.Column<string>(type: "jsonb", nullable: true),
                    clusters_json = table.Column<string>(type: "jsonb", nullable: true),
                    competitors_json = table.Column<string>(type: "jsonb", nullable: true),
                    novelty_json = table.Column<string>(type: "jsonb", nullable: true),
                    gaps_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idea_validations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_idea_validations_idea_hash_created_at_utc",
                table: "idea_validations",
                columns: new[] { "idea_hash", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_idea_validations_status",
                table: "idea_validations",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idea_validations");
        }
    }
}
