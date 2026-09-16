using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Nordiska.Modules.Banking.Contracts.Requests;

namespace Nordiska.FrontendApi.Contracts.Requests;

/// <summary>
/// Query parameters for filtering, searching and paginating transaction records.
/// </summary>
public record TransactionQueryRequest(
    long? AccountId = null,
    IReadOnlyList<long>? AccountIds = null,
    string? Type = null,
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
