namespace Nordiska.BuildingBlocks.Database;

    public sealed record DatabaseDetails(
    string Schema,
    string RuntimeConnectionStringName,
    string MigrationConnectionStringName,
    string MigrationsHistoryTable = "__EFMigrationsHistory");