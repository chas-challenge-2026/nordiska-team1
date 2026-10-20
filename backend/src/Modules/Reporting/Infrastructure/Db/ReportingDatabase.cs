using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Reporting.Infrastructure.Db;

internal static class ReportingDatabase
{
    internal static DatabaseDetails Details { get; } = new(
        Schema: "reporting",
        RuntimeConnectionStringName: "ReportingDatabase",
        MigrationConnectionStringName: "ReportingMigrationDatabase");
}