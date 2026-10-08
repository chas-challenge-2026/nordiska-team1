namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Representation of a term/condition awaiting customer acceptance.
/// </summary>
/// <param name="AcceptanceId">Unique acceptance record ID.</param>
/// <param name="TermId">Unique term ID.</param>
/// <param name="Code">Unique machine-readable identifier for the agreement (e.g. 'TERMS_GENERAL_2026', 'PRIVACY_POLICY').</param>
/// <param name="Version">Incremental version number of the terms (e.g. 1, 2).</param>
/// <param name="Title">Descriptive human-readable title of the agreement.</param>
/// <param name="DocumentId">Associated PDF document ID in the document hub.</param>
/// <param name="EffectiveFrom">Date and time from which these terms become legally effective.</param>
/// <param name="PublishedAt">Timestamp when the terms were published.</param>
/// <param name="Status">Current acceptance status: 'Pending', 'Accepted', or 'Rejected'.</param>
/// <param name="CreatedAt">Timestamp when this acceptance requirement was created for the customer.</param>
/// <param name="DownloadUrl">API path to download and review the full terms document.</param>
public sealed record PendingTermResponse(
    long AcceptanceId,
    long TermId,
    string Code,
    int Version,
    string Title,
    long DocumentId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset PublishedAt,
    string Status,
    DateTimeOffset? CreatedAt,
    string DownloadUrl
)
{
    public bool IsRead => false;
    public string Category => "Terms";
}