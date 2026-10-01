using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxReportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tax_report_jobs",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DownloadUrl = table.Column<string>(type: "text", nullable: true),
                    ErrorCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_report_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_Action",
                schema: "reporting",
                table: "audit_entries",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_CreatedAt",
                schema: "reporting",
                table: "audit_entries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_UserId_CreatedAt",
                schema: "reporting",
                table: "audit_entries",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_report_jobs_AccountId_Year",
                schema: "reporting",
                table: "tax_report_jobs",
                columns: new[] { "AccountId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_report_jobs_CustomerId_CreatedAt",
                schema: "reporting",
                table: "tax_report_jobs",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_report_jobs_Status_CreatedAt",
                schema: "reporting",
                table: "tax_report_jobs",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tax_report_jobs",
                schema: "reporting");

            migrationBuilder.DropIndex(
                name: "IX_audit_entries_Action",
                schema: "reporting",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "IX_audit_entries_CreatedAt",
                schema: "reporting",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "IX_audit_entries_UserId_CreatedAt",
                schema: "reporting",
                table: "audit_entries");
        }
    }
}
