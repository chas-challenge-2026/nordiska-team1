using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnualTaxReportFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tax_reports",
                schema: "reporting");

            migrationBuilder.CreateTable(
                name: "tax_reports",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    TotalInterestMinor = table.Column<long>(type: "bigint", nullable: false),
                    TaxDeductedMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tax_report_documents",
                schema: "reporting",
                columns: table => new
                {
                    TaxReportId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_report_documents", x => new { x.TaxReportId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_tax_report_documents_tax_reports_TaxReportId",
                        column: x => x.TaxReportId,
                        principalSchema: "reporting",
                        principalTable: "tax_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_report_jobs",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaxReportId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_report_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tax_report_jobs_tax_reports_TaxReportId",
                        column: x => x.TaxReportId,
                        principalSchema: "reporting",
                        principalTable: "tax_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tax_reports_AccountId_TaxYear",
                schema: "reporting",
                table: "tax_reports",
                columns: new[] { "AccountId", "TaxYear" },
                unique: true);

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
                name: "IX_tax_report_documents_DocumentId",
                schema: "reporting",
                table: "tax_report_documents",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_report_jobs_Status_CreatedAt",
                schema: "reporting",
                table: "tax_report_jobs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_report_jobs_TaxReportId",
                schema: "reporting",
                table: "tax_report_jobs",
                column: "TaxReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tax_report_documents",
                schema: "reporting");

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

            migrationBuilder.DropTable(
                name: "tax_reports",
                schema: "reporting");

            migrationBuilder.CreateTable(
                name: "tax_reports",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DownloadUrl = table.Column<string>(type: "text", nullable: true),
                    Signature = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_reports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tax_reports_AccountId_Year",
                schema: "reporting",
                table: "tax_reports",
                columns: new[] { "AccountId", "Year" });
        }
    }
}
