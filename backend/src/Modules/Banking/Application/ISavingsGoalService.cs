using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Application;

public interface ISavingsGoalService
{
    Task<SavingsGoal?> GetByIdAsync(long goalId, CancellationToken cancellationToken = default);
    Task<AutomateSavingsGoalResponse> AutomateAsync(long goalId, AutomateSavingsGoalRequest request, long customerId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<bool> CancelAutomationAsync(long goalId, long customerId, bool isAdmin = false, CancellationToken cancellationToken = default);
}
