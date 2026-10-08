using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class SavingsGoalRepository(BankingDbContext db) : ISavingsGoalRepository
{
    public async Task<SavingsGoal?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await db.SavingsGoals
            .Include(g => g.Account)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<List<SavingsGoal>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        return await db.SavingsGoals
            .AsNoTracking()
            .Include(g => g.Account)
            .Where(g => g.CustomerId == customerId)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SavingsGoal>> GetByAccountIdAsync(long accountId, CancellationToken cancellationToken = default)
    {
        return await db.SavingsGoals
            .AsNoTracking()
            .Include(g => g.Account)
            .Where(g => g.AccountId == accountId)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CreateAsync(SavingsGoal goal, CancellationToken cancellationToken = default)
    {
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        return goal.Id;
    }

    public async Task<bool> UpdateAsync(SavingsGoal goal, CancellationToken cancellationToken = default)
    {
        db.SavingsGoals.Update(goal);
        var changed = await db.SaveChangesAsync(cancellationToken);
        return changed > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var goal = await db.SavingsGoals.FindAsync([id], cancellationToken);
        if (goal is null) return false;

        db.SavingsGoals.Remove(goal);
        var changed = await db.SaveChangesAsync(cancellationToken);
        return changed > 0;
    }
}