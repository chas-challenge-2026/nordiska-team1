using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nordiska.Modules.Faq.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddFaqRelationshipAndViewLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedFaqIds",
                schema: "faq",
                table: "faq_entries");

            migrationBuilder.CreateTable(
                name: "faq_relationship",
                schema: "faq",
                columns: table => new
                {
                    relation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    related_relation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faq_relationship", x => new { x.relation_id, x.related_relation_id });
                });

            migrationBuilder.CreateTable(
                name: "faq_view_log",
                schema: "faq",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    faq_entry_id = table.Column<int>(type: "integer", nullable: false),
                    session_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    viewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faq_view_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_faq_relationship_related_relation_id",
                schema: "faq",
                table: "faq_relationship",
                column: "related_relation_id");

            migrationBuilder.CreateIndex(
                name: "IX_faq_view_log_faq_entry_id_viewed_at",
                schema: "faq",
                table: "faq_view_log",
                columns: new[] { "faq_entry_id", "viewed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_faq_view_log_viewed_at",
                schema: "faq",
                table: "faq_view_log",
                column: "viewed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "faq_relationship",
                schema: "faq");

            migrationBuilder.DropTable(
                name: "faq_view_log",
                schema: "faq");

            migrationBuilder.AddColumn<int[]>(
                name: "RelatedFaqIds",
                schema: "faq",
                table: "faq_entries",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);
        }
    }
}
