using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Faq.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddFaqLanguageAndTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "faq",
                table: "faq_entries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "faq",
                table: "faq_entries",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "sv");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "faq",
                table: "faq_entries",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "faq",
                table: "faq_entries");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "faq",
                table: "faq_entries");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "faq",
                table: "faq_entries");
        }
    }
}
