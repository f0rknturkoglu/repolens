using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RepoLens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_documents",
                columns: table => new
                {
                    repository_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    topics = table.Column<string>(type: "text", nullable: true),
                    primary_language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    readme_text = table.Column<string>(type: "text", nullable: true),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    embedding_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    embedding_model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    embedding_created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_documents", x => x.repository_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_search_documents_content_hash",
                table: "search_documents",
                column: "content_hash");

            migrationBuilder.CreateIndex(
                name: "IX_search_documents_embedding_hash_embedding_model",
                table: "search_documents",
                columns: new[] { "embedding_hash", "embedding_model" });
            migrationBuilder.Sql(
                "ALTER TABLE search_documents ADD COLUMN search_vector tsvector GENERATED ALWAYS AS ("
                + " setweight(to_tsvector('english', coalesce(name, '')), 'A')"
                + " || setweight(to_tsvector('english', coalesce(topics, '')), 'B')"
                + " || setweight(to_tsvector('english', coalesce(description, '')), 'C')"
                + " || setweight(to_tsvector('english', coalesce(readme_text, '')), 'D')) STORED");
            migrationBuilder.Sql(
                "ALTER TABLE search_documents ADD COLUMN embedding vector");
            migrationBuilder.Sql(
                "CREATE INDEX ix_search_documents_search_vector ON search_documents USING gin (search_vector)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_documents");
        }
    }
}
