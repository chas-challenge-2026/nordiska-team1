using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.SqlConfigurations;

public sealed class TaxReportJobConfiguration
    : IEntityTypeConfiguration<TaxReportJob>
{
    public void Configure(
        EntityTypeBuilder<TaxReportJob> builder)
    {
        builder.ToTable("tax_report_jobs");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.Id)
            .ValueGeneratedOnAdd();

        builder.Property(job => job.TaxReportId)
            .IsRequired();

        builder.Property(job => job.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(job => job.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(job => job.CreatedAt)
            .IsRequired();

        builder.Property(job => job.AvailableAt)
            .IsRequired();

        builder.Property(job => job.LockedBy)
            .HasMaxLength(100);

        builder.Property(job => job.LeaseExpiresAt);

        builder.Property(job => job.StartedAt);

        builder.Property(job => job.CompletedAt);

        builder.Property(job => job.LastError)
            .HasMaxLength(2000);

        builder.HasOne<TaxReport>()
            .WithMany()
            .HasForeignKey(job => job.TaxReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(job => new
        {
            job.Status,
            job.AvailableAt
        });

        builder.HasIndex(job => new
        {
            job.Status,
            job.LeaseExpiresAt
        });

        builder.HasIndex(job => job.TaxReportId);
    }
}
