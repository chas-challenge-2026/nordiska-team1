using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTypeRateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_type_rate_histories",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_type_rate_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_type_rate_histories_account_type_configs_AccountType",
                        column: x => x.AccountType,
                        principalSchema: "banking",
                        principalTable: "account_type_configs",
                        principalColumn: "AccountType",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "banking",
                table: "account_type_rate_histories",
                columns: new[] { "Id", "AccountType", "CreatedAtUtc", "EffectiveFromUtc", "EffectiveToUtc", "InterestRate" },
                values: new object[,]
                {
                    { 1L, "flex", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0.035000m },
                    { 2L, "fix", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0.041000m },
                    { 3L, "standard", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0.025000m },
                    { 4L, "saving", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0.035000m },
                    { 5L, "premium", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0.040000m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_type_rate_histories_AccountType_EffectiveFromUtc",
                schema: "banking",
                table: "account_type_rate_histories",
                columns: new[] { "AccountType", "EffectiveFromUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_type_rate_histories",
                schema: "banking");
        }
    }
}
