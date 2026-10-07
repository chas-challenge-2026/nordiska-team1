using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFavoriteToSavingsAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                schema: "banking",
                table: "savings_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFavorite",
                schema: "banking",
                table: "savings_accounts");
        }
    }
}
