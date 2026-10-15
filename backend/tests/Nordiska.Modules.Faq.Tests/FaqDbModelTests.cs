using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;

namespace Nordiska.Modules.Faq.Tests;

// Building the model never opens a connection, so no database is needed here.
public class FaqDbModelTests
{
    private static FaqDbContext CreateContext()
        => new(new DbContextOptionsBuilder<FaqDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);

    [Theory]
    [InlineData(typeof(FaqEntry), "faq_entries")]
    [InlineData(typeof(FaqSearchLog), "faq_search_log")]
    [InlineData(typeof(FaqViewLog), "faq_view_log")]
    [InlineData(typeof(FaqRelationship), "faq_relationship")]
    public void Entity_IsMappedToExpectedTableInFaqSchema(Type clrType, string table)
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(clrType);

        Assert.NotNull(entity);
        Assert.Equal(table, entity.GetTableName());
        Assert.Equal("faq", entity.GetSchema());
    }

    [Theory]
    [InlineData(typeof(FaqEntry), nameof(FaqEntry.Question), 500)]
    [InlineData(typeof(FaqEntry), nameof(FaqEntry.Answer), 2000)]
    [InlineData(typeof(FaqEntry), nameof(FaqEntry.Category), 200)]
    [InlineData(typeof(FaqEntry), nameof(FaqEntry.Keywords), 500)]
    [InlineData(typeof(FaqEntry), nameof(FaqEntry.Language), 10)]
    [InlineData(typeof(FaqSearchLog), nameof(FaqSearchLog.Language), 10)]
    [InlineData(typeof(FaqSearchLog), nameof(FaqSearchLog.SessionHash), 64)]
    [InlineData(typeof(FaqViewLog), nameof(FaqViewLog.SessionHash), 64)]
    public void Property_HasExpectedMaxLength(Type clrType, string property, int maxLength)
    {
        using var db = CreateContext();

        var prop = db.Model.FindEntityType(clrType)!.FindProperty(property);

        Assert.NotNull(prop);
        Assert.Equal(maxLength, prop.GetMaxLength());
        Assert.False(prop.IsNullable);
    }

    [Theory]
    [InlineData(typeof(FaqSearchLog), nameof(FaqSearchLog.NormalizedQuery), "normalized_query")]
    [InlineData(typeof(FaqSearchLog), nameof(FaqSearchLog.SearchedAt), "searched_at")]
    [InlineData(typeof(FaqViewLog), nameof(FaqViewLog.FaqEntryId), "faq_entry_id")]
    [InlineData(typeof(FaqViewLog), nameof(FaqViewLog.ViewedAt), "viewed_at")]
    [InlineData(typeof(FaqRelationship), nameof(FaqRelationship.RelatedRelationId), "related_relation_id")]
    [InlineData(typeof(FaqRelationship), nameof(FaqRelationship.SortOrder), "sort_order")]
    public void Property_HasExpectedColumnName(Type clrType, string property, string column)
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(clrType)!;
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());

        Assert.Equal(column, entity.FindProperty(property)!.GetColumnName(table));
    }

    [Fact]
    public void FaqEntry_HasDefaultsAndRelationIdIndex()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(FaqEntry))!;

        Assert.Equal("sv", entity.FindProperty(nameof(FaqEntry.Language))!.GetDefaultValue());
        Assert.Equal(0, entity.FindProperty(nameof(FaqEntry.HelpfulCount))!.GetDefaultValue());
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(FaqEntry.RelationId) }));
    }

    [Fact]
    public void FaqRelationship_UsesCompositeKey()
    {
        using var db = CreateContext();

        var key = db.Model.FindEntityType(typeof(FaqRelationship))!.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(
            new[] { nameof(FaqRelationship.RelationId), nameof(FaqRelationship.RelatedRelationId) },
            key.Properties.Select(p => p.Name));
    }

    [Fact]
    public void LogEntities_HaveLookupIndexes()
    {
        using var db = CreateContext();

        var searchLog = db.Model.FindEntityType(typeof(FaqSearchLog))!;
        var viewLog = db.Model.FindEntityType(typeof(FaqViewLog))!;

        Assert.Contains(searchLog.GetIndexes(), index =>
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(FaqSearchLog.NormalizedQuery), nameof(FaqSearchLog.Language) }));
        Assert.Contains(viewLog.GetIndexes(), index =>
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(FaqViewLog.FaqEntryId), nameof(FaqViewLog.ViewedAt) }));
    }

    [Fact]
    public void Context_ExposesAllDbSets()
    {
        using var db = CreateContext();

        Assert.NotNull(db.FaqEntries);
        Assert.NotNull(db.FaqSearchLogs);
        Assert.NotNull(db.FaqRelationships);
        Assert.NotNull(db.FaqViewLogs);
    }
}
