namespace Nordiska.Modules.Banking.Application;

public interface ISavingsGoalDepositRepository
{
    Task<SavingsGoalDepositResult> DepositAsync(
        long savingsGoalId,
        long sourceAccountId,
        decimal amount,
        long customerId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);
}