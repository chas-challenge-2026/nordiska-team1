using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Inbox.Infrastructure.Db;

public sealed class InboxDbContextFactory
    : ModuleDesignTimeDbContextFactory<InboxDbContext>
{
    protected override DatabaseDetails Database =>
        InboxDatabase.Details;

    protected override InboxDbContext CreateContext(
        DbContextOptions<InboxDbContext> options)
    {
        return new InboxDbContext(options);
    }
}