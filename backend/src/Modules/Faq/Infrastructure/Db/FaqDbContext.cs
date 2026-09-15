using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Infrastructure.Db;

public sealed class FaqDbContext(
    DbContextOptions<FaqDbContext> options)
    : DbContext(options)
{
    public DbSet<FaqEntry> FaqEntries => Set<FaqEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(FaqDatabase.Details.Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(FaqDbContext).Assembly);
    }
}