namespace Bank.Modules.Banking.Domain;

public sealed class Transaction
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }

    public SavingsAccount Account { get; set; } = null!;
}
