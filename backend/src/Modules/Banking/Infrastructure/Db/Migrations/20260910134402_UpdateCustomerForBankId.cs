using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCustomerForBankId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_customers_NormalizedEmail",
                schema: "banking",
                table: "customers");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "banking",
                table: "customers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

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

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "banking",
                table: "customers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

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
