using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class AuditEntryConfig
    : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Action)
            .IsRequired();

        builder.Property(x => x.UserId);

        builder.Property(x => x.Details)
            .IsRequired();

        builder.Property(x => x.Signature)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}