using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class TransactionRepository(BankingDbContext db) : ITransactionRepository
{
    public async Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
    {
        var query = db.LedgerEntries.AsNoTracking().Where(l => !l.IsPlanned).AsQueryable();
        if (accountId.HasValue) query = query.Where(l => l.AccountId == accountId.Value);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<LedgerEntry>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var query = db.LedgerEntries.AsNoTracking().AsQueryable();

        if (parameters.AccountIds != null && parameters.AccountIds.Count > 0)
        {
            query = query.Where(l => parameters.AccountIds.Contains(l.AccountId));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Type))
        {
            var typeLower = parameters.Type.Trim().ToLowerInvariant();
            query = query.Where(l => l.Type.ToLower() == typeLower);
        }

        if (parameters.FromDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= parameters.FromDate.Value);
        }

        if (parameters.ToDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= parameters.ToDate.Value);
        }

        if (parameters.MinAmount.HasValue)
        {
            query = query.Where(l => l.Amount >= parameters.MinAmount.Value);
        }

        if (parameters.MaxAmount.HasValue)
        {
            query = query.Where(l => l.Amount <= parameters.MaxAmount.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(l => l.Type.ToLower().Contains(term) 
                                     || (l.Label != null && l.Label.ToLower().Contains(term))
                                     || l.Id.ToString().Contains(term) 
                                     || l.AccountId.ToString().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var isAsc = string.Equals(parameters.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = parameters.SortBy?.Trim().ToLowerInvariant();

        query = sortBy switch
        {
            "amount" => isAsc ? query.OrderBy(l => l.Amount).ThenBy(l => l.Id) : query.OrderByDescending(l => l.Amount).ThenByDescending(l => l.Id),
            _ => isAsc ? query.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id) : query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
        };

        var page = parameters.NormalizedPage;
        var pageSize = parameters.NormalizedPageSize;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<LedgerEntry>.Create(items, totalCount, page, pageSize);
    }

    public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => db.LedgerEntries.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id != 0) throw new ArgumentException("Only new ledger entries can be created.", nameof(entry));
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await db.LedgerEntries.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (item is null) return false;

        db.LedgerEntries.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
