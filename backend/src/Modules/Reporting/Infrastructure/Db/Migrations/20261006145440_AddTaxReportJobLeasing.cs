using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxReportJobLeasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tax_report_jobs_Status_CreatedAt",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "reporting",
                table: "tax_report_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAt",
                schema: "reporting",
                table: "tax_report_jobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "reporting",
                table: "tax_report_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: "reporting",
                table: "tax_report_jobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                schema: "reporting",
                table: "tax_report_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                schema: "reporting",
                table: "tax_report_jobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                schema: "reporting",
                table: "tax_report_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_report_jobs_Status_AvailableAt",
                schema: "reporting",
                table: "tax_report_jobs",
                columns: new[] { "Status", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_report_jobs_Status_LeaseExpiresAt",
                schema: "reporting",
                table: "tax_report_jobs",
                columns: new[] { "Status", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tax_report_jobs_Status_AvailableAt",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.DropIndex(
                name: "IX_tax_report_jobs_Status_LeaseExpiresAt",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.DropColumn(
                name: "AvailableAt",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.DropColumn(
                name: "LastError",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                schema: "reporting",
                table: "tax_report_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_tax_report_jobs_Status_CreatedAt",
                schema: "reporting",
                table: "tax_report_jobs",
                columns: new[] { "Status", "CreatedAt" });
        }
    }
}
