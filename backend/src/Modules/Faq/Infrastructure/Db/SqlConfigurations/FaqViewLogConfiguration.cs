using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Infrastructure.Db.SqlConfigurations;

public sealed class FaqViewLogConfiguration
    : IEntityTypeConfiguration<FaqViewLog>
{
    public void Configure(EntityTypeBuilder<FaqViewLog> builder)
    {
        builder.ToTable("faq_view_log");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FaqEntryId)
            .HasColumnName("faq_entry_id");

        builder.Property(x => x.SessionHash)
            .HasColumnName("session_hash")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.ViewedAt)
            .HasColumnName("viewed_at")
            .IsRequired();

        builder.HasIndex(x => new { x.FaqEntryId, x.ViewedAt });

        builder.HasIndex(x => x.ViewedAt);
    }
}
