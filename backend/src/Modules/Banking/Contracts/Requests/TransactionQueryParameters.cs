using System;
using System.Collections.Generic;

namespace Nordiska.Modules.Banking.Contracts.Requests;

/// <summary>
/// Parameters for filtering, searching, sorting and paginating transaction records.
/// </summary>
public record TransactionQueryParameters(
    IReadOnlyList<long>? AccountIds = null,
    string? Type = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    string? SearchTerm = null,
    string? SortBy = "createdAt",
    string? SortOrder = "desc",
    int Page = 1,
    int PageSize = 20)
{
    /// <summary>
    /// Gets sanitized page number (minimum 1).
    /// </summary>
    public int NormalizedPage => Page < 1 ? 1 : Page;

    /// <summary>
    /// Gets sanitized page size (between 1 and 100).
    /// </summary>
    public int NormalizedPageSize => PageSize switch
    {
        < 1 => 20,
        > 100 => 100,
        _ => PageSize
    };
}
