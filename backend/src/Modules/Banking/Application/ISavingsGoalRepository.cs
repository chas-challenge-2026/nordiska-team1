using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Application;

public interface ISavingsGoalRepository
{
    Task<SavingsGoal?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<List<SavingsGoal>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default);
    Task<List<SavingsGoal>> GetByAccountIdAsync(long accountId, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(SavingsGoal goal, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(SavingsGoal goal, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}