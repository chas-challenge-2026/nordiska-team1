namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Result of a term acceptance operation.
/// </summary>
public sealed record TermAcceptanceResult(
    long AcceptanceId,
    long TermId,
    long CustomerId,
    string Status,
    DateTimeOffset? AcceptedAt,
    bool Success,
    string Message
);