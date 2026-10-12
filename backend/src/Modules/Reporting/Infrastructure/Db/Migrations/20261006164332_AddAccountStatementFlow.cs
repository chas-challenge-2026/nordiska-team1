using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountStatementFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_statements",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OpeningBalanceMinor = table.Column<long>(type: "bigint", nullable: false),
                    ClosingBalanceMinor = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_statements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "account_statement_documents",
                schema: "reporting",
                columns: table => new
                {
                    AccountStatementId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_statement_documents", x => new { x.AccountStatementId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_account_statement_documents_account_statements_AccountState~",
                        column: x => x.AccountStatementId,
                        principalSchema: "reporting",
                        principalTable: "account_statements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_statement_documents_generated_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "reporting",
                        principalTable: "generated_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_statement_entries",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountStatementId = table.Column<long>(type: "bigint", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLedgerEntryId = table.Column<long>(type: "bigint", nullable: false),
                    BookedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    BalanceAfterMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_statement_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_statement_entries_account_statements_AccountStateme~",
                        column: x => x.AccountStatementId,
                        principalSchema: "reporting",
                        principalTable: "account_statements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_statement_jobs",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountStatementId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_statement_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_statement_jobs_account_statements_AccountStatementId",
                        column: x => x.AccountStatementId,
                        principalSchema: "reporting",
                        principalTable: "account_statements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_statement_documents_DocumentId",
                schema: "reporting",
                table: "account_statement_documents",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_account_statement_entries_AccountStatementId_SequenceNumber",
                schema: "reporting",
                table: "account_statement_entries",
                columns: new[] { "AccountStatementId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_statement_entries_AccountStatementId_SourceLedgerEn~",
                schema: "reporting",
                table: "account_statement_entries",
                columns: new[] { "AccountStatementId", "SourceLedgerEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_statement_jobs_AccountStatementId",
                schema: "reporting",
                table: "account_statement_jobs",
                column: "AccountStatementId");

            migrationBuilder.CreateIndex(
                name: "IX_account_statement_jobs_Status_AvailableAt",
                schema: "reporting",
                table: "account_statement_jobs",
                columns: new[] { "Status", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_account_statement_jobs_Status_LeaseExpiresAt",
                schema: "reporting",
                table: "account_statement_jobs",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_account_statements_CustomerId_AccountId_PeriodStart_PeriodE~",
                schema: "reporting",
                table: "account_statements",
                columns: new[] { "CustomerId", "AccountId", "PeriodStart", "PeriodEnd" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_statement_documents",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "account_statement_entries",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "account_statement_jobs",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "account_statements",
                schema: "reporting");
        }
    }
}
