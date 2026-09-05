using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Faq.Infrastructure.Db;

public sealed class FaqDbContextFactory
    : ModuleDesignTimeDbContextFactory<FaqDbContext>
{
    protected override DatabaseDetails Database =>
        FaqDatabase.Details;

    protected override FaqDbContext CreateContext(
        DbContextOptions<FaqDbContext> options)
    {
        return new FaqDbContext(options);
    }
}