using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

public interface ISavingsGoalService
{
    Task<SavingsGoalResponse?> GetByIdAsync(long goalId, long customerId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<List<SavingsGoalResponse>> GetGoalsAsync(long customerId, long? accountId = null, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<SavingsGoalResponse> CreateAsync(CreateSavingsGoalRequest request, long customerId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<SavingsGoalResponse> UpdateAsync(long goalId, UpdateSavingsGoalRequest request, long customerId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long goalId, long customerId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<AutomateSavingsGoalResponse> AutomateAsync(long goalId, AutomateSavingsGoalRequest request, long customerId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<bool> CancelAutomationAsync(long goalId, long customerId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<SavingsGoalDepositResponse> DepositAsync(long goalId, DepositToSavingsGoalRequest request, long customerId, bool isAdmin = false, CancellationToken cancellationToken = default);
}
