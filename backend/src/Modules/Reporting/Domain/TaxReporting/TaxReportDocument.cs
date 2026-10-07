namespace Nordiska.Modules.Reporting.Domain;

public sealed class TaxReportDocument
{
    private TaxReportDocument()
    {}

    private TaxReportDocument(
        long taxReportId,
        long documentId,
        DateTimeOffset createdAt)
    {
        TaxReportId = taxReportId;
        DocumentId = documentId;
        CreatedAt = createdAt;
    }

    public long TaxReportId { get; private set; }

    public long DocumentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TaxReportDocument Create(
        long taxReportId,
        long documentId,
        DateTimeOffset createdAt)
    {
        return new TaxReportDocument(
            taxReportId,
            documentId,
            createdAt);
    }
}