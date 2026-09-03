using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.DbConfigs;

public sealed class ReportDbContext : DbContext
{
    public ReportDbContext(
        DbContextOptions<ReportDbContext> options)
        : base(options) {}

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

