using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class BankingDbContext : DbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<AccountTypeConfig> AccountTypeConfigs =>
        Set<AccountTypeConfig>();

    public DbSet<SavingsAccount> SavingsAccounts =>
        Set<SavingsAccount>();

    public DbSet<LedgerEntry> LedgerEntries =>
        Set<LedgerEntry>();

    public DbSet<Notification> Notifications =>
        Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(BankingDbDetails.Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(BankingDbContext).Assembly);
    }
}