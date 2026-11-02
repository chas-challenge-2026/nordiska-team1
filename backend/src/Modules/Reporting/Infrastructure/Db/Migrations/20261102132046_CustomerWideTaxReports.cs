using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class CustomerWideTaxReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tax_reports_AccountId_TaxYear",
                schema: "reporting",
                table: "tax_reports");

            migrationBuilder.AlterColumn<long>(
                name: "AccountId",
                schema: "reporting",
                table: "tax_reports",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_tax_reports_AccountId_TaxYear",
                schema: "reporting",
                table: "tax_reports",
                columns: new[] { "AccountId", "TaxYear" },
                unique: true,
                filter: "\"AccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tax_reports_CustomerId_TaxYear",
                schema: "reporting",
                table: "tax_reports",
                columns: new[] { "CustomerId", "TaxYear" },
                unique: true,
                filter: "\"AccountId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tax_reports_AccountId_TaxYear",
                schema: "reporting",
                table: "tax_reports");

            migrationBuilder.DropIndex(
                name: "IX_tax_reports_CustomerId_TaxYear",
                schema: "reporting",
                table: "tax_reports");

            migrationBuilder.AlterColumn<long>(
                name: "AccountId",
                schema: "reporting",
                table: "tax_reports",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_reports_AccountId_TaxYear",
                schema: "reporting",
                table: "tax_reports",
                columns: new[] { "AccountId", "TaxYear" },
                unique: true);
        }
    }
}
