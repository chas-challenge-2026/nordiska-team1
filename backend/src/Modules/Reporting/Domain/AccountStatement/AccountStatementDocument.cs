namespace Nordiska.Modules.Reporting.Domain;

public sealed class AccountStatementDocument
{
    private AccountStatementDocument()
    {
    }

    private AccountStatementDocument(
        long accountStatementId,
        long documentId,
        DateTimeOffset createdAt)
    {
        AccountStatementId = accountStatementId;
        DocumentId = documentId;
        CreatedAt = createdAt;
    }

    public long AccountStatementId { get; private set; }

    public long DocumentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AccountStatementDocument Create(
        long accountStatementId,
        long documentId,
        DateTimeOffset createdAt)
    {
        return new AccountStatementDocument(
            accountStatementId,
            documentId,
            createdAt);
    }
}
