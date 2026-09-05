using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure.Db.SqlConfigurations;

public sealed class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Version)
            .IsRowVersion();

        builder.Property(x => x.PersonalNum)
            .IsRequired()
            .HasMaxLength(12); //todo: kom på en smartarte lösming att sätta dessa värden och kommunicera med dto:s ? ? 

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property<string>("NormalizedEmail")
            .HasMaxLength(254)
            .HasComputedColumnSql(
                "lower(btrim(\"Email\"))",
                stored: true);

        builder.HasIndex("NormalizedEmail")
            .IsUnique()
            .HasDatabaseName("UX_customers_NormalizedEmail");

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.PersonalNum)
            .IsUnique();

        builder.HasIndex(x => x.Email)
            .IsUnique();
    }
}