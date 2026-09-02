using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RepoLens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscoveryRepository : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "repositories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    github_id = table.Column<long>(type: "bigint", nullable: false),
                    owner = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    html_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    default_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    primary_language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    stars = table.Column<int>(type: "integer", nullable: false),
                    forks = table.Column<int>(type: "integer", nullable: false),
                    open_issues = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    is_fork = table.Column<bool>(type: "boolean", nullable: false),
                    license_spdx = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pushed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    discovered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repositories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_repositories_full_name",
                table: "repositories",
                column: "full_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repositories_github_id",
                table: "repositories",
                column: "github_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "repositories");
        }
    }
}
