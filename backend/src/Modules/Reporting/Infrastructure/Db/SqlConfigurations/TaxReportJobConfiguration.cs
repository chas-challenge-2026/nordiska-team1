using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.SqlConfigurations;

public sealed class TaxReportJobConfiguration
    : IEntityTypeConfiguration<TaxReportJob>
{
    public void Configure(EntityTypeBuilder<TaxReportJob> builder)
    {
        builder.ToTable("tax_report_jobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TaxReportId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne<TaxReport>()
            .WithMany()
            .HasForeignKey(x => x.TaxReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new
        {
            x.Status,
            x.CreatedAt
        });

        builder.HasIndex(x => x.TaxReportId);
    }
}