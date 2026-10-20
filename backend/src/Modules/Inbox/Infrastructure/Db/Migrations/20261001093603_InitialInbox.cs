using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nordiska.Modules.Inbox.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class InitialInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inbox");

            migrationBuilder.CreateTable(
                name: "customer_notifications",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: true),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "feed_items",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Preview = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ActionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feed_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "message_boxes",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_boxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_documents",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FirstOpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AvailableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_documents_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "inbox",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "terms",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_terms_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "inbox",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "message_threads",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageBoxId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_threads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_message_threads_message_boxes_MessageBoxId",
                        column: x => x.MessageBoxId,
                        principalSchema: "inbox",
                        principalTable: "message_boxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "term_acceptances",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeclinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_term_acceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_term_acceptances_terms_TermId",
                        column: x => x.TermId,
                        principalSchema: "inbox",
                        principalTable: "terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "message_thread_states",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThreadId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Folder = table.Column<int>(type: "integer", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_thread_states", x => x.Id);
                    table.ForeignKey(
                        name: "FK_message_thread_states_message_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalSchema: "inbox",
                        principalTable: "message_threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThreadId = table.Column<long>(type: "bigint", nullable: false),
                    SenderType = table.Column<int>(type: "integer", nullable: false),
                    SenderCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ReplyAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_messages_message_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalSchema: "inbox",
                        principalTable: "message_threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_documents_CustomerId_DocumentId",
                schema: "inbox",
                table: "customer_documents",
                columns: new[] { "CustomerId", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_documents_CustomerId_PublishedAt",
                schema: "inbox",
                table: "customer_documents",
                columns: new[] { "CustomerId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_documents_DocumentId",
                schema: "inbox",
                table: "customer_documents",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_notifications_CustomerId_ReadAt_CreatedAt",
                schema: "inbox",
                table: "customer_notifications",
                columns: new[] { "CustomerId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_notifications_TargetType_TargetId",
                schema: "inbox",
                table: "customer_notifications",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_Sha256",
                schema: "inbox",
                table: "documents",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_documents_SourceType_SourceId",
                schema: "inbox",
                table: "documents",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_StorageKey",
                schema: "inbox",
                table: "documents",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feed_items_CustomerId_OccurredAt",
                schema: "inbox",
                table: "feed_items",
                columns: new[] { "CustomerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_feed_items_ItemType_SourceId_CustomerId",
                schema: "inbox",
                table: "feed_items",
                columns: new[] { "ItemType", "SourceId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_boxes_CustomerId",
                schema: "inbox",
                table: "message_boxes",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_thread_states_CustomerId_Folder_ReadAt",
                schema: "inbox",
                table: "message_thread_states",
                columns: new[] { "CustomerId", "Folder", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_message_thread_states_ThreadId_CustomerId",
                schema: "inbox",
                table: "message_thread_states",
                columns: new[] { "ThreadId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_threads_MessageBoxId_LastMessageAt",
                schema: "inbox",
                table: "message_threads",
                columns: new[] { "MessageBoxId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_ThreadId_SentAt",
                schema: "inbox",
                table: "messages",
                columns: new[] { "ThreadId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_term_acceptances_CustomerId_Status",
                schema: "inbox",
                table: "term_acceptances",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_term_acceptances_TermId_CustomerId",
                schema: "inbox",
                table: "term_acceptances",
                columns: new[] { "TermId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terms_Code_Version",
                schema: "inbox",
                table: "terms",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terms_DocumentId",
                schema: "inbox",
                table: "terms",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_documents",
                schema: "inbox");

            migrationBuilder.DropTable(
                name: "customer_notifications",
                schema: "inbox");

            migrationBuilder.DropTable(
                name: "feed_items",
                schema: "inbox");

            migrationBuilder.DropTable(
                name: "message_thread_states",
                schema: "inbox");

            migrationBuilder.DropTable(
                name: "messages",
                schema: "inbox");

            migrationBuilder.DropTable(
                name: "term_acceptances",
                schema: "inbox");

            migrationBuilder.DropTable(
                name: "message_threads",
                schema: "inbox");

            migrationBuilder.DropTable(
                name: "terms",
                schema: "inbox");

            migrationBuilder.DropTable(
                name: "message_boxes",
                schema: "inbox");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "inbox");
        }
    }
}
