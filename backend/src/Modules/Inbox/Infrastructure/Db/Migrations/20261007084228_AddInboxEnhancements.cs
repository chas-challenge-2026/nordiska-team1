using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Inbox.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "inbox",
                table: "message_threads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInformationOnly",
                schema: "inbox",
                table: "message_threads",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                schema: "inbox",
                table: "message_threads");

            migrationBuilder.DropColumn(
                name: "IsInformationOnly",
                schema: "inbox",
                table: "message_threads");
        }
    }
}
