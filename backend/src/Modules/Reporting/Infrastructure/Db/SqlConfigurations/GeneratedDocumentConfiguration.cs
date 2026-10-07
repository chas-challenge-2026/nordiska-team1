using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.SqlConfigurations;

public sealed class GeneratedDocumentConfiguration
    : IEntityTypeConfiguration<GeneratedDocument>
{
    public void Configure(
        EntityTypeBuilder<GeneratedDocument> builder)
    {
        builder.ToTable("generated_documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id)
            .ValueGeneratedOnAdd();

        builder.Property(document => document.DocumentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(document => document.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(document => document.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(document => document.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(document => document.ByteLength)
            .IsRequired();

        builder.Property(document => document.Sha256Hash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(document => document.CreatedAt)
            .IsRequired();

        builder.HasIndex(document => document.StorageKey)
            .IsUnique();

        builder.HasIndex(document => document.Sha256Hash);
    }
}