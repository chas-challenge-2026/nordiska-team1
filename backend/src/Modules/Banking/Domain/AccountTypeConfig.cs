namespace Nordiska.Modules.Banking.Domain;

public sealed class AccountTypeConfig
{
    public string AccountType { get; set; } = string.Empty;
    public decimal InterestRate { get; set; }
    public string Description { get; set; } = string.Empty;

    public ICollection<SavingsAccount> SavingsAccounts { get; set; } = [];
}