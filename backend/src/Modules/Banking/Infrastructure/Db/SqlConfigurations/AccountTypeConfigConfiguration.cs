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
    }
}