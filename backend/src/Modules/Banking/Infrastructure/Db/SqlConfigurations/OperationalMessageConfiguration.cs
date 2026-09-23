using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure.Db.SqlConfigurations;

public sealed class OperationalMessageConfiguration : IEntityTypeConfiguration<OperationalMessage>
{
    public void Configure(EntityTypeBuilder<OperationalMessage> builder)
    {
        builder.ToTable("operational_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TitleSv)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TitleEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.MessageSv)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.MessageEn)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Severity)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("info");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.StartDate);

        builder.Property(x => x.EndDate);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.Priority);
    }
}
