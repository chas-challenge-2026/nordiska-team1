using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operational_messages",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TitleSv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MessageSv = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MessageEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "info"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_messages_IsActive",
                schema: "banking",
                table: "operational_messages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_operational_messages_Priority",
                schema: "banking",
                table: "operational_messages",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_messages",
                schema: "banking");
        }
    }
}
