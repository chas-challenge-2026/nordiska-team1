using System;
using System.Collections.Generic;

namespace Nordiska.BuildingBlocks.Database;

/// <summary>
/// Generic container for offset-paginated query results.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">Items on the requested page. Empty when no items match or the page is beyond the result set.</param>
/// <param name="TotalCount">Total number of matching items across every page.</param>
/// <param name="Page">Current one-based page number.</param>
/// <param name="PageSize">Requested maximum number of items per page.</param>
/// <param name="TotalPages">Total number of pages. Zero when <c>TotalCount</c> is zero.</param>
/// <param name="HasNextPage"><c>true</c> when a later page exists.</param>
/// <param name="HasPreviousPage"><c>true</c> when an earlier non-empty page exists.</param>
public record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage)
{
    /// <summary>
    /// Creates a new <see cref="PagedResult{T}"/> instance with computed pagination metadata.
    /// </summary>
    /// <param name="items">The items on the current page.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    /// <param name="page">The current page index (1-based).</param>
    /// <param name="pageSize">The requested page size.</param>
    public static PagedResult<T> Create(IReadOnlyCollection<T> items, int totalCount, int page, int pageSize)
    {
        var validPage = page < 1 ? 1 : page;
        var validPageSize = pageSize < 1 ? 20 : pageSize;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)validPageSize);
        var hasNextPage = validPage < totalPages;
        var hasPreviousPage = validPage > 1 && totalPages > 0;

        return new PagedResult<T>(
            items,
            totalCount,
            validPage,
            validPageSize,
            totalPages,
            hasNextPage,
            hasPreviousPage);
    }
}
