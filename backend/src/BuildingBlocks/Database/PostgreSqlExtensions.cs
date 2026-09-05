using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nordiska.BuildingBlocks.Database;

/// <summary>
/// Reusable code to create migration tables for a schema.
/// Used by DbContexts. 
/// </summary>

public static class PostgreSqlExtensions
{
    public static DbContextOptionsBuilder UseModulePostgres(
        this DbContextOptionsBuilder options,
        string connectionString,
        DatabaseDetails database)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(database.Schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            database.MigrationsHistoryTable);

        return options.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable(
                    database.MigrationsHistoryTable,
                    database.Schema);
            });
    }

    public static IServiceCollection AddModulePostgresDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        DatabaseDetails database)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(database);

        var connectionString = GetRequiredConnectionString(
            configuration,
            database.RuntimeConnectionStringName);

        services.AddDbContext<TContext>(options =>
        {
            options.UseModulePostgres(connectionString, database);
        });

        return services;
    }

    private static string GetRequiredConnectionString(
        IConfiguration configuration,
        string connectionStringName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionStringName);

        var connectionString =
            configuration.GetConnectionString(connectionStringName);

        return !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : throw new InvalidOperationException(
                $"Connection string '{connectionStringName}' is missing.");
    }
}







