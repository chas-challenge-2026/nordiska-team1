using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.DbConfigs;

public sealed class ReportDbContext(
    DbContextOptions<ReportDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<TaxReport> TaxReports => Set<TaxReport>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(ReportDbConstants.Schema ?? "Faq");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ReportDbContext).Assembly);
    }
}

