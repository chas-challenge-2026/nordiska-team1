using System;

namespace Nordiska.Modules.Banking.Domain;

public sealed class AccountTypeRateHistory
{
    public long Id { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public decimal InterestRate { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public AccountTypeConfig AccountTypeConfig { get; set; } = null!;
}
