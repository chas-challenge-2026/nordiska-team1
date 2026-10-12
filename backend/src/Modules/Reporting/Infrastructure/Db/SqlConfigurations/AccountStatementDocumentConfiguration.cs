using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.SqlConfigurations;

public sealed class AccountStatementDocumentConfiguration
    : IEntityTypeConfiguration<AccountStatementDocument>
{
    public void Configure(
        EntityTypeBuilder<AccountStatementDocument> builder)
    {
        builder.ToTable("account_statement_documents");

        builder.HasKey(link => new
        {
            link.AccountStatementId,
            link.DocumentId
        });

        builder.Property(link => link.AccountStatementId)
            .IsRequired();

        builder.Property(link => link.DocumentId)
            .IsRequired();

        builder.Property(link => link.CreatedAt)
            .IsRequired();

        builder.HasOne<AccountStatement>()
            .WithMany()
            .HasForeignKey(link => link.AccountStatementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<GeneratedDocument>()
            .WithMany()
            .HasForeignKey(link => link.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => link.DocumentId);
    }
}
