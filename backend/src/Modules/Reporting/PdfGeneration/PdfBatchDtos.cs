using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nordiska.Modules.Reporting.PdfGeneration;

public record CustomerBatchEnvelope(
    [property: JsonPropertyName("$schema_version")] string SchemaVersion,
    [property: JsonPropertyName("customer_id")] ulong CustomerId,
    [property: JsonPropertyName("customer_name")] string CustomerName,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("documents")] IReadOnlyList<PdfDocumentEnvelope> Documents
);

public record PdfDocumentEnvelope(
    [property: JsonPropertyName("document_id")] string DocumentId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("document")] object Document
);

public record AnnualTaxReportPayload(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("tax_year")] string TaxYear,
    [property: JsonPropertyName("account_number")] string AccountNumber,
    [property: JsonPropertyName("account_name")] string AccountName,
    [property: JsonPropertyName("total_interest_earned")] string TotalInterestEarned,
    [property: JsonPropertyName("preliminary_tax_deducted")] string PreliminaryTaxDeducted,
    [property: JsonPropertyName("reported_to_authority")] string ReportedToAuthority
);

public record AccountStatementPayload(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("account_number")] string AccountNumber,
    [property: JsonPropertyName("account_name")] string AccountName,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("period")] string Period,
    [property: JsonPropertyName("opening_balance")] string OpeningBalance,
    [property: JsonPropertyName("closing_balance")] string ClosingBalance,
    [property: JsonPropertyName("transactions")] IReadOnlyList<StatementTransactionPayload> Transactions
);

public record StatementTransactionPayload(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("amount_minor")] long AmountMinor,
    [property: JsonPropertyName("amount_display")] string AmountDisplay,
    [property: JsonPropertyName("balance_after_display")] string BalanceAfterDisplay
);
