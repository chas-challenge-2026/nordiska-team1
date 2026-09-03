using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Infrastructure.DbConfigs;


public sealed class FaqEntryConfiguration : IEntityTypeConfiguration<FaqEntry>
{
    public void Configure(EntityTypeBuilder<FaqEntry> builder)
    {
        builder.ToTable("faq_entries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Question)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Answer)
            .IsRequired();

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.HelpfulCount)
            .IsRequired();

        builder.Property(x => x.Keywords)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(x => x.Category);
    }
}