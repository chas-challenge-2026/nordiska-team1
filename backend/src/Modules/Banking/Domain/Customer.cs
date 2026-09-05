using Microsoft.AspNetCore.Identity;

namespace Nordiska.Modules.Banking.Domain;


public sealed class Customer : IdentityUser <long>
{
    public string PersonalNum { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    

    public ICollection<SavingsAccount> SavingsAccounts { get; set; } = [];
}