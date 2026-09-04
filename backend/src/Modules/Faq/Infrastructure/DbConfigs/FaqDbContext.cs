using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Infrastructure.DbConfigs;

public sealed class FaqDbContext(
    DbContextOptions<FaqDbContext> options) : DbContext(options)
{
    public DbSet<FaqEntry> FaqEnties => Set<FaqEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(FaqDbConstants.Schema ?? "Faq");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(FaqDbContext).Assembly);
    }
}