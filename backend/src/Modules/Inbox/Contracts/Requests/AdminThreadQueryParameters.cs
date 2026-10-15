namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Query parameters for admin to list, filter and search support threads across all customers.
/// </summary>
/// <param name="CustomerId">Filter by specific customer ID.</param>
/// <param name="Status">Filter by status ('Open', 'Closed').</param>
/// <param name="SearchTerm">Filter by search keyword in subject or message bodies.</param>
/// <param name="Page">Page number (1-based).</param>
/// <param name="PageSize">Items per page (1-100, default 20).</param>
public sealed record AdminThreadQueryParameters(
    long? CustomerId = null,
    string? Status = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20);