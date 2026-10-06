namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Query parameters for listing and filtering customer documents.
/// </summary>
public sealed record DocumentQueryParameters(
    int? Year = null,
    string? DocumentType = null,
    int Page = 1,
    int PageSize = 20
);