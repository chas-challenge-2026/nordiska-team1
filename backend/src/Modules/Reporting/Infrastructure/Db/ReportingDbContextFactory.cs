using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Reporting.Infrastructure.Db;

public sealed class ReportingDbContextFactory
    : ModuleDesignTimeDbContextFactory<ReportingDbContext>
{
    protected override DatabaseDetails Database =>
        ReportingDatabase.Details;

    protected override ReportingDbContext CreateContext(
        DbContextOptions<ReportingDbContext> options)
    {
        return new ReportingDbContext(options);
    }
}