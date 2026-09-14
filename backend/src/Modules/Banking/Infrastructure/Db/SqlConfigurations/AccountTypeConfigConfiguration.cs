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
                AccountType = "Sparkonto Flex",
                InterestRate = 0.035000m,
                Description = "Rörligt sparkonto med fria insättningar och uttag (FLEX)."
            },
            new AccountTypeConfig
            {
                AccountType = "Fasträntekonto Fix",
                InterestRate = 0.041000m,
                Description = "Fasträntekonto med bunden ränta för långsiktigt sparande (FIX)."
            },
            new AccountTypeConfig
            {
                AccountType = "Standard",
                InterestRate = 0.025000m,
                Description = "Standard sparkonto med fria insättningar och uttag."
            },
            new AccountTypeConfig
            {
                AccountType = "Savings",
                InterestRate = 0.035000m,
                Description = "Förmånligt sparkonto för långsiktigt sparande."
            },
            new AccountTypeConfig
            {
                AccountType = "Premium",
                InterestRate = 0.040000m,
                Description = "Premium sparkonto med bankens högsta sparränta."
            }
        );
    }
}