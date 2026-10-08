using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Inbox.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class BackfillInboxFeedItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO inbox.feed_items
                    ("CustomerId", "ItemType", "SourceId", "Title", "Preview", "Priority", "ActionRequired", "OccurredAt", "ReadAt")
                SELECT
                    state."CustomerId",
                    1,
                    thread."Id",
                    thread."Subject",
                    LEFT(last_message."Body", 1000),
                    1,
                    FALSE,
                    thread."LastMessageAt",
                    state."ReadAt"
                FROM inbox.message_thread_states AS state
                INNER JOIN inbox.message_threads AS thread ON thread."Id" = state."ThreadId"
                LEFT JOIN LATERAL (
                    SELECT message."Body"
                    FROM inbox.messages AS message
                    WHERE message."ThreadId" = thread."Id" AND message."RevokedAt" IS NULL
                    ORDER BY message."SentAt" DESC
                    LIMIT 1
                ) AS last_message ON TRUE
                ON CONFLICT ("ItemType", "SourceId", "CustomerId") DO NOTHING;

                INSERT INTO inbox.feed_items
                    ("CustomerId", "ItemType", "SourceId", "Title", "Preview", "Priority", "ActionRequired", "OccurredAt", "ReadAt")
                SELECT
                    notification."CustomerId",
                    5,
                    notification."Id",
                    notification."Title",
                    LEFT(notification."Body", 1000),
                    CASE notification."Priority" WHEN 4 THEN 3 WHEN 3 THEN 2 ELSE 1 END,
                    notification."Priority" = 4,
                    notification."CreatedAt",
                    notification."ReadAt"
                FROM inbox.customer_notifications AS notification
                WHERE notification."TargetType" IS DISTINCT FROM 2
                ON CONFLICT ("ItemType", "SourceId", "CustomerId") DO NOTHING;

                INSERT INTO inbox.feed_items
                    ("CustomerId", "ItemType", "SourceId", "Title", "Preview", "Priority", "ActionRequired", "OccurredAt", "ReadAt")
                SELECT
                    customer_document."CustomerId",
                    2,
                    document."Id",
                    document."Title",
                    LEFT(document."DocumentType" || ' • ' || document."FileName", 1000),
                    1,
                    FALSE,
                    customer_document."PublishedAt",
                    customer_document."FirstOpenedAt"
                FROM inbox.customer_documents AS customer_document
                INNER JOIN inbox.documents AS document ON document."Id" = customer_document."DocumentId"
                WHERE document."Status" <> 3
                ON CONFLICT ("ItemType", "SourceId", "CustomerId") DO NOTHING;

                INSERT INTO inbox.feed_items
                    ("CustomerId", "ItemType", "SourceId", "Title", "Preview", "Priority", "ActionRequired", "OccurredAt", "ReadAt")
                SELECT
                    acceptance."CustomerId",
                    3,
                    term."Id",
                    term."Title",
                    LEFT('Version ' || term."Version" || '. ' ||
                        CASE WHEN acceptance."Status" = 2 THEN 'Godkänd.' ELSE 'Inväntar digital acceptans.' END, 1000),
                    CASE WHEN acceptance."Status" = 2 THEN 1 ELSE 2 END,
                    acceptance."Status" = 1,
                    term."PublishedAt",
                    CASE WHEN acceptance."Status" = 2 THEN acceptance."AcceptedAt" ELSE NULL END
                FROM inbox.term_acceptances AS acceptance
                INNER JOIN inbox.terms AS term ON term."Id" = acceptance."TermId"
                WHERE term."Status" = 2
                ON CONFLICT ("ItemType", "SourceId", "CustomerId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
