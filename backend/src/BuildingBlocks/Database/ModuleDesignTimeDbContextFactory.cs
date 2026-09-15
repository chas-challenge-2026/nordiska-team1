using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nordiska.BuildingBlocks.Database;

public abstract class ModuleDesignTimeDbContextFactory<TContext>
    : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    protected abstract DatabaseDetails Database { get; }

    protected abstract TContext CreateContext(
        DbContextOptions<TContext> options);

    public TContext CreateDbContext(string[] args)
    {
        var variableName =
            $"ConnectionStrings__{
                Database.MigrationConnectionStringName}";

        var connectionString =
            Environment.GetEnvironmentVariable(variableName)
            ?? throw new InvalidOperationException(
                $" '{variableName}' needs to be set before running EF Core migrations.");

        var options = new DbContextOptionsBuilder<TContext>();

        options.UseModulePostgres(connectionString, Database);

        return CreateContext(options.Options);
    }
}