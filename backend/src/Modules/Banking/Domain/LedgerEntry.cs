namespace Nordiska.Modules.Banking.Domain;

public sealed class LedgerEntry
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }

    public SavingsAccount Account { get; set; } = null!;
}