using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxReportAuthorityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorityReference",
                schema: "reporting",
                table: "tax_reports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReportedAt",
                schema: "reporting",
                table: "tax_reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportingAuthority",
                schema: "reporting",
                table: "tax_reports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportingStatus",
                schema: "reporting",
                table: "tax_reports",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotReported");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorityReference",
                schema: "reporting",
                table: "tax_reports");

            migrationBuilder.DropColumn(
                name: "ReportedAt",
                schema: "reporting",
                table: "tax_reports");

            migrationBuilder.DropColumn(
                name: "ReportingAuthority",
                schema: "reporting",
                table: "tax_reports");

            migrationBuilder.DropColumn(
                name: "ReportingStatus",
                schema: "reporting",
                table: "tax_reports");
        }
    }
}
