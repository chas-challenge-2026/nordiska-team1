using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAccountTypeConfigsSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "Fasträntekonto Fix");

            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "Premium");

            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "Savings");

            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "Sparkonto Flex");

            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "Standard");

            migrationBuilder.InsertData(
                schema: "banking",
                table: "account_type_configs",
                columns: new[] { "AccountType", "Description", "InterestRate" },
                values: new object[,]
                {
                    { "fix", "Fixed-term savings account with 3-month lock-in.", 0.041000m },
                    { "flex", "Flexible savings account with variable interest rate.", 0.035000m },
                    { "premium", "Premium savings account with top-tier interest rate.", 0.040000m },
                    { "saving", "High-yield savings account.", 0.035000m },
                    { "standard", "Standard savings account for everyday savings.", 0.025000m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "fix");

            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "flex");

            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "premium");

            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "saving");

            migrationBuilder.DeleteData(
                schema: "banking",
                table: "account_type_configs",
                keyColumn: "AccountType",
                keyValue: "standard");

            migrationBuilder.InsertData(
                schema: "banking",
                table: "account_type_configs",
                columns: new[] { "AccountType", "Description", "InterestRate" },
                values: new object[,]
                {
                    { "Fasträntekonto Fix", "Fasträntekonto med bunden ränta för långsiktigt sparande (FIX).", 0.041000m },
                    { "Premium", "Premium sparkonto med bankens högsta sparränta.", 0.040000m },
                    { "Savings", "Förmånligt sparkonto för långsiktigt sparande.", 0.035000m },
                    { "Sparkonto Flex", "Rörligt sparkonto med fria insättningar och uttag (FLEX).", 0.035000m },
                    { "Standard", "Standard sparkonto med fria insättningar och uttag.", 0.025000m }
                });
        }
    }
}
