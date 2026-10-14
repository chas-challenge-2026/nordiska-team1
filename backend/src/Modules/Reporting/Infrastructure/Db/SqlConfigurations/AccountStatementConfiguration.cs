using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.SqlConfigurations;

public sealed class AccountStatementConfiguration
    : IEntityTypeConfiguration<AccountStatement>
{
    public void Configure(
        EntityTypeBuilder<AccountStatement> builder)
    {
        builder.ToTable("account_statements");

        builder.HasKey(statement => statement.Id);

        builder.Property(statement => statement.Id)
            .ValueGeneratedOnAdd();

        builder.Property(statement => statement.CustomerId)
            .IsRequired();

        builder.Property(statement => statement.AccountId)
            .IsRequired();

        builder.Property(statement => statement.PeriodStart)
            .IsRequired();

        builder.Property(statement => statement.PeriodEnd)
            .IsRequired();

        builder.Property(statement => statement.CustomerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(statement => statement.AccountNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(statement => statement.AccountName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(statement => statement.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(statement => statement.OpeningBalanceMinor)
            .IsRequired();

        builder.Property(statement => statement.ClosingBalanceMinor)
            .IsRequired();

        builder.Property(statement => statement.SnapshotAt)
            .IsRequired();

        builder.Property(statement => statement.SchemaVersion)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(statement => statement.PayloadHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasMany(statement => statement.Entries)
            .WithOne(entry => entry.AccountStatement)
            .HasForeignKey(entry => entry.AccountStatementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(statement => statement.Entries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(statement => new
        {
            statement.CustomerId,
            statement.AccountId,
            statement.PeriodStart,
            statement.PeriodEnd
        });
    }
}
