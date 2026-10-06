using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Nordiska.Modules.Banking.Contracts.Requests;

namespace Nordiska.FrontendApi.Contracts.Requests;

/// <summary>
/// Query parameters for filtering, searching and paginating transaction records.
/// </summary>
/// <param name="AccountId">Filter by a single account ID.</param>
/// <param name="AccountIds">Filter by multiple account IDs.</param>
/// <param name="Type">Filter by transaction type (e.g. 'deposit', 'withdrawal').</param>
/// <param name="IsPlanned">Optional filter on planned status: true for scheduled/planned transactions only, false for executed transactions only, or omit for all.</param>
/// <param name="FromDate">Start date filter (inclusive UTC).</param>
/// <param name="ToDate">End date filter (inclusive UTC).</param>
/// <param name="MinAmount">Minimum transaction amount filter.</param>
/// <param name="MaxAmount">Maximum transaction amount filter.</param>
/// <param name="SearchTerm">Search term matching description, type, or reference ID.</param>
/// <param name="SortBy">Sort column (e.g. 'createdAt' or 'amount'). Default: 'createdAt'.</param>
/// <param name="SortOrder">Sort direction ('asc' or 'desc'). Default: 'desc'.</param>
/// <param name="Page">1-based page index. Default: 1.</param>
/// <param name="PageSize">Page size limit (1 to 100). Default: 20.</param>
public record TransactionQueryRequest(
    long? AccountId = null,
    IReadOnlyList<long>? AccountIds = null,
    string? Type = null,
    bool? IsPlanned = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    [StringLength(100)]
    string? SearchTerm = null,
    string? SortBy = "createdAt",
    string? SortOrder = "desc",
    [Range(1, int.MaxValue)]
    int Page = 1,
    [Range(1, 100)]
    int PageSize = 20)
{
    /// <summary>
    /// Converts to domain query parameters with consolidated account IDs.
    /// </summary>
    /// <param name="effectiveAccountIds">The authorized account IDs to query.</param>
    public TransactionQueryParameters ToDomainParameters(IReadOnlyList<long>? effectiveAccountIds = null)
    {
        return new TransactionQueryParameters(
            AccountIds: effectiveAccountIds ?? GetRequestedAccountIds(),
            Type: Type,
            IsPlanned: IsPlanned,
            FromDate: FromDate,
            ToDate: ToDate,
            MinAmount: MinAmount,
            MaxAmount: MaxAmount,
            SearchTerm: SearchTerm,
            SortBy: SortBy,
            SortOrder: SortOrder,
            Page: Page,
            PageSize: PageSize);
    }

    /// <summary>
    /// Extracts all requested account IDs from both single AccountId and AccountIds list.
    /// </summary>
    public IReadOnlyList<long>? GetRequestedAccountIds()
    {
        var ids = new HashSet<long>();
        if (AccountId.HasValue) ids.Add(AccountId.Value);
        if (AccountIds != null)
        {
            foreach (var id in AccountIds) ids.Add(id);
        }
        return ids.Count > 0 ? ids.ToList() : null;
    }
}
