using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class ReportingDbContext : DbContext
{
    public ReportingDbContext(
        DbContextOptions<ReportingDbContext> options)
        : base(options)
    {
    }

    public DbSet<TaxReport> TaxReports => Set<TaxReport>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(ReportingDbDetails.Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ReportingDbContext).Assembly);
    }
}