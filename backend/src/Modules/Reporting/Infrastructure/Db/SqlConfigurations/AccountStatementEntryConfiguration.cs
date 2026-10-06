using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.SqlConfigurations;

public sealed class AccountStatementEntryConfiguration
    : IEntityTypeConfiguration<AccountStatementEntry>
{
    public void Configure(
        EntityTypeBuilder<AccountStatementEntry> builder)
    {
        builder.ToTable("account_statement_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedOnAdd();

        builder.Property(entry => entry.AccountStatementId)
            .IsRequired();

        builder.Property(entry => entry.SequenceNumber)
            .IsRequired();

        builder.Property(entry => entry.SourceLedgerEntryId)
            .IsRequired();

        builder.Property(entry => entry.BookedAt)
            .IsRequired();

        builder.Property(entry => entry.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(entry => entry.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(entry => entry.AmountMinor)
            .IsRequired();

        builder.Property(entry => entry.BalanceAfterMinor)
            .IsRequired();

        builder.HasIndex(entry => new
        {
            entry.AccountStatementId,
            entry.SequenceNumber
        }).IsUnique();

        builder.HasIndex(entry => new
        {
            entry.AccountStatementId,
            entry.SourceLedgerEntryId
        }).IsUnique();
    }
}
