using Microsoft.EntityFrameworkCore;

namespace Nordiska.BuildingBlocks.Database;

public static class PostgreSqlExtensions
{
    public static DbContextOptionsBuilder UseNordiskaPostgres(
        this DbContextOptionsBuilder options,
        string connectionString,
        string schema)
    {
        options.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    schema);
            });

        return options;
    }
}