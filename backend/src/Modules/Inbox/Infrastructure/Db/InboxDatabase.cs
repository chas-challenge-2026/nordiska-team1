using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Inbox.Infrastructure.Db;

internal static class InboxDatabase
{
    internal static DatabaseDetails Details { get; } = new(
        Schema: "inbox",
        RuntimeConnectionStringName: "InboxDatabase",
        MigrationConnectionStringName: "InboxMigrationDatabase");
}