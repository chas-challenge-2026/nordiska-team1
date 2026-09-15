using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class SeedAccountTypeConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "banking",
                table: "account_type_configs",
                columns: new[] { "AccountType", "Description", "InterestRate" },
                values: new object[,]
                {
                    { "Fasträntekonto Fix", "Fasträntekonto med 3 månaders bindningstid och hög ränta (FIX).", 0.041000m },
                    { "Premium", "Premium sparkonto med högre ränta för större sparande.", 0.037500m },
                    { "Savings", "Förmånligt sparkonto för långsiktigt sparande.", 0.035000m },
                    { "Sparkonto Flex", "Rörligt sparkonto med fria insättningar och uttag (FLEX).", 0.035000m },
                    { "Standard", "Standard sparkonto med fria insättningar och uttag.", 0.025000m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
