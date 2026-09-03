using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class TaxReportConfig
    : IEntityTypeConfiguration<TaxReport>
{
    public void Configure(EntityTypeBuilder<TaxReport> builder)
    {
        builder.ToTable("tax_reports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AccountId)
            .IsRequired();

        builder.Property(x => x.Year)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.DownloadUrl);

        builder.Property(x => x.Signature);

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}