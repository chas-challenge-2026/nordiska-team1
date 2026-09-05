using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Faq.Infrastructure.Db;

internal static class FaqDatabase
{
    internal static DatabaseDetails Details { get; } = new(
        Schema: "faq",
        RuntimeConnectionStringName: "FaqDatabase",
        MigrationConnectionStringName: "FaqMigrationDatabase");
}