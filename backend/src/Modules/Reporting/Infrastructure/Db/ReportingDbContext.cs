using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.Db;

public sealed class ReportingDbContext(
    DbContextOptions<ReportingDbContext> options)
    : DbContext(options)
{
    public DbSet<TaxReport> TaxReports => Set<TaxReport>();

    public DbSet<TaxReportJob> TaxReportJobs => Set<TaxReportJob>();

    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();

    public DbSet<TaxReportDocument> TaxReportDocuments => Set<TaxReportDocument>();

    public DbSet<AccountStatement> AccountStatements => Set<AccountStatement>();

    public DbSet<AccountStatementEntry> AccountStatementEntries => Set<AccountStatementEntry>();

    public DbSet<AccountStatementJob> AccountStatementJobs => Set<AccountStatementJob>();

    public DbSet<AccountStatementDocument> AccountStatementDocuments => Set<AccountStatementDocument>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(ReportingDatabase.Details.Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ReportingDbContext).Assembly);
    }
}
