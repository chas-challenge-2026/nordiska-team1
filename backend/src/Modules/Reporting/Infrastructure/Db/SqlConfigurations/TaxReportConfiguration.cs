using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.SqlConfigurations;

public sealed class TaxReportConfiguration
    : IEntityTypeConfiguration<TaxReport>
{
    public void Configure(EntityTypeBuilder<TaxReport> builder)
    {
        builder.ToTable("tax_reports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CustomerId)
            .IsRequired();

        builder.Property(x => x.AccountId)
            .IsRequired();

        builder.Property(x => x.TaxYear)
            .IsRequired();

        builder.Property(x => x.TotalInterestMinor)
            .IsRequired();

        builder.Property(x => x.TaxDeductedMinor)
            .IsRequired();

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.CustomerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AccountNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.AccountName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.SchemaVersion)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.PayloadHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(x => new
            {
                x.AccountId,
                x.TaxYear
            })
            .IsUnique();

    }
}