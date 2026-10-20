using System.Text.Json.Serialization;

namespace Nordiska.Reporting.Worker.JobHandlers.PdfGeneration;

internal sealed record NativeReportPackage(
    [property: JsonPropertyName("$schema_version")]
    string SchemaVersion,

    [property: JsonPropertyName("customer_id")]
    long CustomerId,

    [property: JsonPropertyName("customer_name")]
    string CustomerName,

    [property: JsonPropertyName("created_at")]
    string CreatedAt,

    [property: JsonPropertyName("documents")]
    IReadOnlyList<NativeDocumentEnvelope> Documents);

internal sealed record NativeDocumentEnvelope(
    [property: JsonPropertyName("document_id")]
    string DocumentId,

    [property: JsonPropertyName("kind")]
    string Kind,

    [property: JsonPropertyName("version")]
    string Version,

    [property: JsonPropertyName("document")]
    object Document);
