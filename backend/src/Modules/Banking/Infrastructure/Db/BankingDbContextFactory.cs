using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;

/// <summary>
/// This is a helper class for EF Core Tools used while creating
/// and applying migrations.
/// EF Core needs a DbContext to create or apply migrations.
/// This class provides two things:
/// 1) MigrationConnectionVariable points to the connection string
/// that logs in as the migrator database role.
/// This prevents migrations from using the lower-permission API role.
/// 2) A fully configured BankingDbContext for EF Core Tools.
/// </summary>
namespace Nordiska.Modules.Banking.Infrastructure.Db;
public sealed class BankingDbContextFactory
    : ModuleDesignTimeDbContextFactory<BankingDbContext>
{
    protected override DatabaseDetails Database =>
        BankingDatabase.Details;

    protected override BankingDbContext CreateContext(
        DbContextOptions<BankingDbContext> options)
    {
        return new BankingDbContext(options);
    }
}