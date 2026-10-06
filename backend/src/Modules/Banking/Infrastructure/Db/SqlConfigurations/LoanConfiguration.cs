using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure.Db.SqlConfigurations;

public sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CustomerId)
            .IsRequired();

        builder.Property(x => x.LoanNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.PrincipalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.OutstandingAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.InterestRate)
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.OpenedAt)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.MaturityDate)
            .HasColumnType("date");

        builder.Property(x => x.InterestAccruedThrough)
            .HasColumnType("date")
            .IsRequired();

        builder.HasIndex(x => x.LoanNumber)
            .IsUnique();

        builder.HasIndex(x => new { x.CustomerId, x.Status });

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
