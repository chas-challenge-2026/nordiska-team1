using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerEntryPeriodIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_AccountId_CreatedAt",
                schema: "banking",
                table: "ledger_entries",
                columns: new[] { "AccountId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_AccountId_CreatedAt",
                schema: "banking",
                table: "ledger_entries");
        }
    }
}
