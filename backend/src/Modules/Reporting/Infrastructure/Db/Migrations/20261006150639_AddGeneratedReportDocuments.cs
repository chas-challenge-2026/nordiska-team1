using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedReportDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generated_documents",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generated_documents_Sha256Hash",
                schema: "reporting",
                table: "generated_documents",
                column: "Sha256Hash");

            migrationBuilder.CreateIndex(
                name: "IX_generated_documents_StorageKey",
                schema: "reporting",
                table: "generated_documents",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tax_report_documents_generated_documents_DocumentId",
                schema: "reporting",
                table: "tax_report_documents",
                column: "DocumentId",
                principalSchema: "reporting",
                principalTable: "generated_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tax_report_documents_generated_documents_DocumentId",
                schema: "reporting",
                table: "tax_report_documents");

            migrationBuilder.DropTable(
                name: "generated_documents",
                schema: "reporting");
        }
    }
}
