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

        builder.Property(x => x.PasswordHash)
            .IsRequired(false)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        var stringListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
            (c1, c2) => c1 != null && c2 != null ? System.Linq.Enumerable.SequenceEqual(c1, c2) : c1 == c2,
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        builder.Property(x => x.OverviewPreference)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
            )
            .Metadata.SetValueComparer(stringListComparer);

        builder.Property(x => x.OverviewPreference)
            .HasDefaultValueSql("'[]'::jsonb");

        builder.HasIndex(x => x.PersonalNum)
            .IsUnique();

        builder.HasIndex(x => x.Email)
            .IsUnique();
    }
}