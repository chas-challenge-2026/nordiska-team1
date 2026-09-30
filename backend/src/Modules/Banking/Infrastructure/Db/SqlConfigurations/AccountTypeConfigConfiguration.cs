using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure.Db.SqlConfigurations;

public sealed class AccountTypeConfigConfiguration : IEntityTypeConfiguration<AccountTypeConfig>
{
    public void Configure(EntityTypeBuilder<AccountTypeConfig> builder)
    {
        builder.ToTable("account_type_configs");

        builder.HasKey(x => x.AccountType);

        builder.Property(x => x.AccountType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.InterestRate)
            .IsRequired()
            .HasPrecision(9, 6);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasData(
            new AccountTypeConfig
            {
                AccountType = "flex",
                InterestRate = 0.035000m,
                Description = "Flexible savings account with variable interest rate."
            },
            new AccountTypeConfig
            {
                AccountType = "fix",
                InterestRate = 0.041000m,
                Description = "Fixed-term savings account with 3-month lock-in."
            },
            new AccountTypeConfig
            {
                AccountType = "standard",
                InterestRate = 0.025000m,
                Description = "Standard savings account for everyday savings."
            },
            new AccountTypeConfig
            {
                AccountType = "saving",
                InterestRate = 0.035000m,
                Description = "High-yield savings account."
            },
            new AccountTypeConfig
            {
                AccountType = "premium",
                InterestRate = 0.040000m,
                Description = "Premium savings account with top-tier interest rate."
            }
        );
    }
}