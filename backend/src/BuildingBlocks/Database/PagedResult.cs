using System;
using System.Collections.Generic;

namespace Nordiska.BuildingBlocks.Database;

/// <summary>
/// Generic container for offset-paginated query results.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
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