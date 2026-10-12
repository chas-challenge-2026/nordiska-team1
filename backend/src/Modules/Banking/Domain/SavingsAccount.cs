using System;
using System.Collections.Generic;

namespace Nordiska.Modules.Banking.Domain;

public sealed class SavingsAccount
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "SEK";
    public decimal Balance { get; set; }
    public decimal InterestRate { get; set; }
    public string Status { get; set; } = "active";
    public bool IsFavorite { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public AccountTypeConfig AccountTypeConfig { get; set; } = null!;
    public ICollection<LedgerEntry> Transactions { get; set; } = new List<LedgerEntry>();
    public ICollection<SavingsGoal> SavingsGoals { get; set; } = new List<SavingsGoal>();
}
