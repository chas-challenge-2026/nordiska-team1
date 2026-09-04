using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Infrastructure.DbConfigs;

namespace Nordiska.BuildingBlocks.Database;

public static class PostgreSqlExtensions
{
    public static DbContextOptionsBuilder UseNordiskaPostgres(
        this DbContextOptionsBuilder options,
        string connectionString,
        string schema)
    {

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new MisshapenConnectionStringException(connectionString);
        if (string.IsNullOrWhiteSpace(schema))
            throw new InvalidDataException("Schema cannot be empty");
            
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