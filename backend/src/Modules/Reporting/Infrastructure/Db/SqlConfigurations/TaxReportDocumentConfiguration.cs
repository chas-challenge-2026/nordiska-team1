using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.SqlConfigurations;

public sealed class TaxReportDocumentConfiguration
    : IEntityTypeConfiguration<TaxReportDocument>
{
    public void Configure(EntityTypeBuilder<TaxReportDocument> builder)
    {
        builder.ToTable("tax_report_documents");

        builder.HasKey(x => new
        {
            x.TaxReportId,
            x.DocumentId
        });

        builder.Property(x => x.TaxReportId)
            .IsRequired();

        builder.Property(x => x.DocumentId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne<TaxReport>()
            .WithMany()
            .HasForeignKey(x => x.TaxReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DocumentId);
    }
}