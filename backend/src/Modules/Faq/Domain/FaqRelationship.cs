namespace Nordiska.Modules.Faq.Domain;

// Links whole articles through RelationId, so one link covers every language version
public sealed class FaqRelationship
{
    public Guid RelationId { get; private set; }
    public Guid RelatedRelationId { get; private set; }
    public int SortOrder { get; private set; }

    private FaqRelationship() { }

    public static FaqRelationship Create(Guid relationId, Guid relatedRelationId, int sortOrder)
    {
        return new FaqRelationship
        {
            RelationId = relationId,
            RelatedRelationId = relatedRelationId,
            SortOrder = sortOrder
        };
    }
}
