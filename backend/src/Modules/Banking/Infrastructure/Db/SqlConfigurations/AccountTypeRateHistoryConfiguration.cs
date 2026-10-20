using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure.Db.SqlConfigurations;

public sealed class AccountTypeRateHistoryConfiguration : IEntityTypeConfiguration<AccountTypeRateHistory>
{
    public void Configure(EntityTypeBuilder<AccountTypeRateHistory> builder)
    {
        builder.ToTable("account_type_rate_histories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AccountType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.InterestRate)
            .IsRequired()
            .HasPrecision(9, 6);

        builder.Property(x => x.EffectiveFromUtc)
            .IsRequired();

        builder.Property(x => x.EffectiveToUtc);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.AccountTypeConfig)
            .WithMany(x => x.RateHistories)
            .HasForeignKey(x => x.AccountType)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AccountType, x.EffectiveFromUtc });

        var initialEffectiveDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new AccountTypeRateHistory
            {
                Id = 1,
                AccountType = "flex",
                InterestRate = 0.035000m,
                EffectiveFromUtc = initialEffectiveDate,
                EffectiveToUtc = null,
                CreatedAtUtc = initialEffectiveDate
            },
            new AccountTypeRateHistory
            {
                Id = 2,
                AccountType = "fix",
                InterestRate = 0.041000m,
                EffectiveFromUtc = initialEffectiveDate,
                EffectiveToUtc = null,
                CreatedAtUtc = initialEffectiveDate
            },
            new AccountTypeRateHistory
            {
                Id = 3,
                AccountType = "standard",
                InterestRate = 0.025000m,
                EffectiveFromUtc = initialEffectiveDate,
                EffectiveToUtc = null,
                CreatedAtUtc = initialEffectiveDate
            },
            new AccountTypeRateHistory
            {
                Id = 4,
                AccountType = "saving",
                InterestRate = 0.035000m,
                EffectiveFromUtc = initialEffectiveDate,
                EffectiveToUtc = null,
                CreatedAtUtc = initialEffectiveDate
            },
            new AccountTypeRateHistory
            {
                Id = 5,
                AccountType = "premium",
                InterestRate = 0.040000m,
                EffectiveFromUtc = initialEffectiveDate,
                EffectiveToUtc = null,
                CreatedAtUtc = initialEffectiveDate
            }
        );
    }
}
