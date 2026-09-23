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
            .ValueGeneratedNever();

        builder.Property(x => x.CustomerId)
            .IsRequired();

        builder.Property(x => x.AccountId)
            .IsRequired();

        builder.Property(x => x.Year)
            .IsRequired();
        
        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.DownloadUrl);

        builder.Property(x => x.ErrorCode);

        builder.Property(x => x.ErrorMessage);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.Property(x => x.StartedAt);

        builder.Property(x => x.CompletedAt);

        builder.HasIndex(x => new { x.AccountId, x.Year });
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt });
    }
}