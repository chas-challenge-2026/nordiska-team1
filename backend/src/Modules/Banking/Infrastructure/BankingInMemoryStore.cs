using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure;

// Temporary in-memory store for demonstration purposes. To be replaced with a proper database context.
internal static class BankingInMemoryStore
{
    public static readonly List<SavingsAccount> SavingsAccounts = Enumerable.Range(1, 3)
        .Select(i => new SavingsAccount
        {
            Id = i,
            CustomerId = i,
            AccountNumber = $"SE{1000 + i}",
            AccountType = i % 2 == 0 ? "Premium" : "Standard",
            Balance = 1000m * i,
            InterestRate = 0.5m,
            CreatedAt = DateTime.UtcNow.AddDays(-i)
        }).ToList();

    public static readonly List<LedgerEntry> Ledger = Enumerable.Range(1, 5)
        .Select(i => new LedgerEntry
        {
            Id = i,
            AccountId = (i % 3) + 1,
            Type = i % 2 == 0 ? "deposit" : "withdrawal",
            Amount = 100m * i,
            CreatedAt = DateTime.UtcNow.AddDays(-i)
        }).ToList();

    private static long _nextAccountId = SavingsAccounts.Max(a => a.Id) + 1;
    private static long _nextLedgerId = Ledger.Max(l => l.Id) + 1;

    private static readonly object _accountLock = new();
    private static readonly object _ledgerLock = new();

    public static long NextAccountId()
    {
        lock (_accountLock)
        {
            return _nextAccountId++;
        }
    }

    public static long NextLedgerId()
    {
        lock (_ledgerLock)
        {
            return _nextLedgerId++;
        }
    }
}

namespace Nordiska.Modules.Banking.Infrastructure;

internal static class BankingInMemoryStore
{
    public static readonly List<SavingsAccount> SavingsAccounts = Enumerable.Range(1, 3)
        .Select(i => new SavingsAccount
        {
            Id = i,
            CustomerId = i,
            AccountNumber = $"SE{1000 + i}",
            AccountType = i % 2 == 0 ? "Premium" : "Standard",
            Balance = 1000m * i,
            InterestRate = 0.5m,
            CreatedAt = DateTime.UtcNow.AddDays(-i)
        }).ToList();

    public static readonly List<LedgerEntry> Ledger = Enumerable.Range(1, 5)
        .Select(i => new LedgerEntry
        {
            Id = i,
            AccountId = (i % 3) + 1,
            Type = i % 2 == 0 ? "deposit" : "withdrawal",
            Amount = 100m * i,
            CreatedAt = DateTime.UtcNow.AddDays(-i)
        }).ToList();

    private static long _nextAccountId = SavingsAccounts.Max(a => a.Id) + 1;
    private static long _nextLedgerId = Ledger.Max(l => l.Id) + 1;

    private static readonly object _accountLock = new();
    private static readonly object _ledgerLock = new();

    public static long NextAccountId()
    {
        lock (_accountLock)
        {
            return _nextAccountId++;
        }
    }

    public static long NextLedgerId()
    {
        lock (_ledgerLock)
        {
            return _nextLedgerId++;
        }
    }
}
