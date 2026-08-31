namespace Bank.Modules.Banking.Domain;

public sealed class Customer
{
    public long Id { get; set; }
    public string PersonalNum { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<SavingsAccount> SavingsAccounts { get; set; } = [];
}
