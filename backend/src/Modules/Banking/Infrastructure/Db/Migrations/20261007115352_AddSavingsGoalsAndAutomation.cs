using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsGoalsAndAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SavingsGoalId",
                schema: "banking",
                table: "ledger_entries",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "savings_goals",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    TargetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings_goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_savings_goals_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "banking",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_savings_goals_savings_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "banking",
                        principalTable: "savings_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_SavingsGoalId",
                schema: "banking",
                table: "ledger_entries",
                column: "SavingsGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_savings_goals_AccountId",
                schema: "banking",
                table: "savings_goals",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_savings_goals_CustomerId",
                schema: "banking",
                table: "savings_goals",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_savings_goals_SavingsGoalId",
                schema: "banking",
                table: "ledger_entries",
                column: "SavingsGoalId",
                principalSchema: "banking",
                principalTable: "savings_goals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_savings_goals_SavingsGoalId",
                schema: "banking",
                table: "ledger_entries");

            migrationBuilder.DropTable(
                name: "savings_goals",
                schema: "banking");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_SavingsGoalId",
                schema: "banking",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "SavingsGoalId",
                schema: "banking",
                table: "ledger_entries");
        }
    }
}
