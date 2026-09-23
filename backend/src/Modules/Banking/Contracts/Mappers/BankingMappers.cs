using System;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Contracts.Mappers;

public static class BankingMappers
{
    public static SavingsAccount ToDomain(this OpenSavingsAccountRequest req)
        => new()
        {
            CustomerId = req.CustomerId,
            AccountNumber = req.AccountNumber ?? string.Empty,
            AccountName = req.AccountName,
            AccountType = req.AccountType,
            Balance = req.InitialDeposit,
            InterestRate = req.InterestRate ?? 0m,
            CreatedAt = DateTime.UtcNow
        };

    public static void ApplyUpdate(this SavingsAccount target, UpdateSavingsAccountRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.AccountType)) target.AccountType = req.AccountType!;
        if (!string.IsNullOrWhiteSpace(req.AccountName)) target.AccountName = req.AccountName;
        // Balance is immutable via update according to ADR 0001 (must go through verified Ledger transactions)
        if (req.InterestRate.HasValue) target.InterestRate = req.InterestRate.Value;
        target.UpdatedAt = DateTime.UtcNow;
    }

    public static SavingsAccountResponse ToResponse(this SavingsAccount acc, decimal? calculatedBalance = null)
        => new(
            acc.Id,
            acc.CustomerId,
            acc.AccountNumber,
            acc.AccountType,
            calculatedBalance ?? acc.Balance,
            acc.InterestRate,
            acc.CreatedAt,
            acc.AccountName,
            acc.UpdatedAt,
            acc.Status
        );

    public static AccountTypeConfig ToDomain(this CreateAccountTypeConfigRequest req)
        => new()
        {
            AccountType = req.AccountType,
            InterestRate = req.InterestRate,
            Description = req.Description ?? string.Empty
        };

    public static void ApplyUpdate(this AccountTypeConfig target, UpdateAccountTypeConfigRequest req)
    {
        if (req.InterestRate.HasValue) target.InterestRate = req.InterestRate.Value;
        if (!string.IsNullOrWhiteSpace(req.Description)) target.Description = req.Description!;
    }

    public static AccountTypeConfigResponse ToResponse(this AccountTypeConfig cfg)
        => new(cfg.AccountType, cfg.InterestRate, cfg.Description);
}
