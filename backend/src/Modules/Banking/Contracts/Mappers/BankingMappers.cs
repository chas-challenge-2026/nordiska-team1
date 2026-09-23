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

    public static OperationalMessage ToDomain(this CreateOperationalMessageRequest req)
        => new()
        {
            TitleSv = req.TitleSv.Trim(),
            TitleEn = req.TitleEn.Trim(),
            MessageSv = req.MessageSv.Trim(),
            MessageEn = req.MessageEn.Trim(),
            Severity = req.Severity.ToLowerInvariant(),
            IsActive = req.IsActive,
            Priority = req.Priority,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            CreatedAt = DateTime.UtcNow
        };

    public static void ApplyUpdate(this OperationalMessage target, UpdateOperationalMessageRequest req)
    {
        target.TitleSv = req.TitleSv.Trim();
        target.TitleEn = req.TitleEn.Trim();
        target.MessageSv = req.MessageSv.Trim();
        target.MessageEn = req.MessageEn.Trim();
        target.Severity = req.Severity.ToLowerInvariant();
        target.IsActive = req.IsActive;
        target.Priority = req.Priority;
        target.StartDate = req.StartDate;
        target.EndDate = req.EndDate;
        target.UpdatedAt = DateTime.UtcNow;
    }

    public static void ApplyPatch(this OperationalMessage target, PatchOperationalMessageRequest req)
    {
        if (req.TitleSv != null) target.TitleSv = req.TitleSv.Trim();
        if (req.TitleEn != null) target.TitleEn = req.TitleEn.Trim();
        if (req.MessageSv != null) target.MessageSv = req.MessageSv.Trim();
        if (req.MessageEn != null) target.MessageEn = req.MessageEn.Trim();
        if (req.Severity != null) target.Severity = req.Severity.Trim().ToLowerInvariant();
        if (req.IsActive.HasValue) target.IsActive = req.IsActive.Value;
        if (req.Priority.HasValue) target.Priority = req.Priority.Value;
        if (req.StartDate != null) target.StartDate = req.StartDate;
        if (req.EndDate != null) target.EndDate = req.EndDate;
        target.UpdatedAt = DateTime.UtcNow;
    }

    public static OperationalMessageResponse ToResponse(this OperationalMessage msg)
        => new(
            msg.Id,
            new BilingualText(msg.TitleSv, msg.TitleEn),
            new BilingualText(msg.MessageSv, msg.MessageEn),
            msg.Severity,
            msg.IsActive,
            msg.Priority,
            msg.StartDate,
            msg.EndDate,
            msg.CreatedAt,
            msg.UpdatedAt
        );
}
