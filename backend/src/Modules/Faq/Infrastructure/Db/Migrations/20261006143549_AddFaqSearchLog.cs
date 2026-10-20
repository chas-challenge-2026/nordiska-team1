using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nordiska.Modules.Faq.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddFaqSearchLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "faq_search_log",
                schema: "faq",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    query = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_query = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    result_count = table.Column<int>(type: "integer", nullable: false),
                    session_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    searched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faq_search_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_faq_search_log_normalized_query_language",
                schema: "faq",
                table: "faq_search_log",
                columns: new[] { "normalized_query", "language" });

            migrationBuilder.CreateIndex(
                name: "IX_faq_search_log_searched_at",
                schema: "faq",
                table: "faq_search_log",
                column: "searched_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "faq_search_log",
                schema: "faq");
        }
    }
}
