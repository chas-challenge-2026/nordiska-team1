namespace Nordiska.Modules.Reporting.Contracts.Responses;

public sealed record AccountStatementAcceptedResponse(
    long AccountStatementId,
    long JobId,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record AccountStatementJobResponse(
    long JobId,
    long AccountStatementId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Error);
