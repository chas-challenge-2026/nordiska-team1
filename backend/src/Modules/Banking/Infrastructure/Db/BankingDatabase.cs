using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Banking.Infrastructure.Db;

internal static class BankingDatabase
{
    internal static DatabaseDetails Details { get; } = new(
        Schema: "banking",
        RuntimeConnectionStringName: "BankingDatabase",
        MigrationConnectionStringName: "BankingMigrationDatabase");
}