using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddEndpointEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS banking.\"UX_customers_NormalizedEmail\";");

            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                schema: "banking",
                table: "savings_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "banking",
                table: "savings_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlanned",
                schema: "banking",
                table: "ledger_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                schema: "banking",
                table: "ledger_entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedDate",
                schema: "banking",
                table: "ledger_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Repeating",
                schema: "banking",
                table: "ledger_entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TargetAccountId",
                schema: "banking",
                table: "ledger_entries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                schema: "banking",
                table: "customers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true,
                oldComputedColumnSql: "lower(btrim(\"Email\"))");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "banking",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "banking",
                table: "customers",
                column: "NormalizedEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                schema: "banking",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "AccountName",
                schema: "banking",
                table: "savings_accounts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "banking",
                table: "savings_accounts");

            migrationBuilder.DropColumn(
                name: "IsPlanned",
                schema: "banking",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "Label",
                schema: "banking",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "PlannedDate",
                schema: "banking",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "Repeating",
                schema: "banking",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "TargetAccountId",
                schema: "banking",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "banking",
                table: "customers");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                schema: "banking",
                table: "customers",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                computedColumnSql: "lower(btrim(\"Email\"))",
                stored: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_customers_NormalizedEmail",
                schema: "banking",
                table: "customers",
                column: "NormalizedEmail",
                unique: true);
        }
    }
}
