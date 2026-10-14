using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Infrastructure.Db.SqlConfigurations;

public sealed class FaqSearchLogConfiguration
    : IEntityTypeConfiguration<FaqSearchLog>
{
    public void Configure(EntityTypeBuilder<FaqSearchLog> builder)
    {
        builder.ToTable("faq_search_log");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Query)
            .HasColumnName("query")
            .IsRequired()
            .HasMaxLength(FaqSearchLogQueue.MaxQueryLength);

        builder.Property(x => x.NormalizedQuery)
            .HasColumnName("normalized_query")
            .IsRequired()
            .HasMaxLength(FaqSearchLogQueue.MaxQueryLength);

        builder.Property(x => x.Language)
            .HasColumnName("language")
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.ResultCount)
            .HasColumnName("result_count");

        builder.Property(x => x.SessionHash)
            .HasColumnName("session_hash")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.SearchedAt)
            .HasColumnName("searched_at")
            .IsRequired();

        builder.HasIndex(x => new { x.NormalizedQuery, x.Language });

        builder.HasIndex(x => x.SearchedAt);
    }
}
