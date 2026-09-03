using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure.DbConfigs;

public sealed class BankingDbContext : DbContext
{
    public BankingDbContext(
        DbContextOptions<BankingDbContext> options)
        : base(options) {}

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<SavingsAccount> SavingsAccounts =>
        Set<SavingsAccount>();

    public DbSet<AccountTypeConfig> AccountTypeConfigs =>
        Set<AccountTypeConfig>();

    public DbSet<LedgerEntry> LedgerEntries =>
        Set<LedgerEntry>();

    public DbSet<Notification> Notifications =>
        Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        /* 
            Sets "banking" as the schema for this module. 
            This is similar to creating a namespace/group but inside the database.
            Which can later be used to set restricted access to the tables in this
            schema and other stuff.  
        */
        modelBuilder.HasDefaultSchema(BankingDbConstants.Schema ?? "banking");
        // Uses the configure files to assemble the tables
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(BankingDbContext).Assembly);
    }
}