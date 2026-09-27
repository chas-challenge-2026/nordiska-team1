using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class OperationalMessageRepository(BankingDbContext db) : IOperationalMessageRepository
{
    public async Task<IEnumerable<OperationalMessage>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.OperationalMessages
            .AsNoTracking()
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<OperationalMessage>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await db.OperationalMessages
            .AsNoTracking()
            .Where(x => x.IsActive &&
                        (!x.StartDate.HasValue || x.StartDate.Value <= now) &&
                        (!x.EndDate.HasValue || x.EndDate.Value >= now))
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationalMessage?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await db.OperationalMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task CreateAsync(OperationalMessage entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        db.OperationalMessages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OperationalMessage entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        db.OperationalMessages.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await db.OperationalMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;

        db.OperationalMessages.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
