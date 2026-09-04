using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure.DbConfigs;

public sealed class SavingsAccountConfiguration
    : IEntityTypeConfiguration<SavingsAccount>
{
    public void Configure(EntityTypeBuilder<SavingsAccount> builder)
    {
        builder.ToTable("savings_accounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AccountNumber)
            .IsRequired()
            .HasMaxLength(34);

        builder.Property(x => x.AccountType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Balance)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(x => x.InterestRate)
            .IsRequired()
            .HasPrecision(9, 6);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.AccountNumber)
            .IsUnique();

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.SavingsAccounts)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AccountTypeConfig)
            .WithMany(x => x.SavingsAccounts)
            .HasForeignKey(x => x.AccountType)
            .OnDelete(DeleteBehavior.Restrict); // todo - fixa ordentlig  delete
    }
}