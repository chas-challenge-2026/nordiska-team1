using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Faq.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddFaqRelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelationId",
                schema: "faq",
                table: "faq_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_faq_entries_RelationId",
                schema: "faq",
                table: "faq_entries",
                column: "RelationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_faq_entries_RelationId",
                schema: "faq",
                table: "faq_entries");

            migrationBuilder.DropColumn(
                name: "RelationId",
                schema: "faq",
                table: "faq_entries");
        }
    }
}
