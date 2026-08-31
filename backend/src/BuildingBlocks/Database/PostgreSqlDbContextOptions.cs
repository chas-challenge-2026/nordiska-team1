using Microsoft.EntityFrameworkCore;

namespace Nordiska.BuildingBlocks.Database;

public static class PostgreSqlDbContextOptions
{
    public static void Configure(
        DbContextOptionsBuilder options,
        string connectionString,
        string migrationsAssembly,
        string migrationsHistorySchema)
    {
        options.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(migrationsAssembly);
                npgsqlOptions.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    migrationsHistorySchema);
            });
    }
}