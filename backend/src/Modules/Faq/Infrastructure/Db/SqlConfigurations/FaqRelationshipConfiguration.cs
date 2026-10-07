using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Infrastructure.Db.SqlConfigurations;

public sealed class FaqRelationshipConfiguration
    : IEntityTypeConfiguration<FaqRelationship>
{
    public void Configure(EntityTypeBuilder<FaqRelationship> builder)
    {
        builder.ToTable("faq_relationship");

        // No foreign keys, RelationId isn't unique in faq_entries since it's shared by the language versions
        builder.HasKey(x => new { x.RelationId, x.RelatedRelationId });

        builder.Property(x => x.RelationId)
            .HasColumnName("relation_id");

        builder.Property(x => x.RelatedRelationId)
            .HasColumnName("related_relation_id");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order");

        builder.HasIndex(x => x.RelatedRelationId);
    }
}
