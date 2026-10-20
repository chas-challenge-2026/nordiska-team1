using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Tests;

public class FaqDomainTests
{
    [Theory]
    [InlineData("abcd")]
    [InlineData("    ")]
    public void Create_QuestionTooShort_Throws(string question)
    {
        Assert.Throws<ArgumentException>(() => FaqEntry.Create(question, "Svar."));
    }

    [Fact]
    public void Create_QuestionTooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => FaqEntry.Create(new string('a', 501), "Svar."));
    }

    [Fact]
    public void Create_EmptyAnswer_Throws()
    {
        Assert.Throws<ArgumentException>(() => FaqEntry.Create("Vad är ränta?", " "));
    }

    [Fact]
    public void Create_CategoryOrKeywordsTooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => FaqEntry.Create("Vad är ränta?", "Svar.", new string('a', 201)));
        Assert.Throws<ArgumentException>(() => FaqEntry.Create("Vad är ränta?", "Svar.", null, new string('a', 501)));
    }

    [Fact]
    public void Create_TrimsTextAndGeneratesRelationId()
    {
        var entry = FaqEntry.Create("  Vad är ränta?  ", "  Svar.  ", " Ränta ", " ränta ");

        Assert.Equal("Vad är ränta?", entry.Question);
        Assert.Equal("Svar.", entry.Answer);
        Assert.Equal("Ränta", entry.Category);
        Assert.Equal("ränta", entry.Keywords);
        Assert.NotEqual(Guid.Empty, entry.RelationId);
    }

    [Fact]
    public void Create_NullCategoryAndKeywords_BecomeEmpty()
    {
        var entry = FaqEntry.Create("Vad är ränta?", "Svar.");

        Assert.Equal(string.Empty, entry.Category);
        Assert.Equal(string.Empty, entry.Keywords);
    }

    [Theory]
    [InlineData(null, "sv")]
    [InlineData("  ", "sv")]
    [InlineData(" EN ", "en")]
    public void SetLanguage_NormalizesValue(string? input, string expected)
    {
        var entry = FaqEntry.Create("Vad är ränta?", "Svar.");

        entry.SetLanguage(input);

        Assert.Equal(expected, entry.Language);
    }

    [Fact]
    public void SetLanguage_TooLong_Throws()
    {
        var entry = FaqEntry.Create("Vad är ränta?", "Svar.");

        Assert.Throws<ArgumentException>(() => entry.SetLanguage(new string('a', 11)));
    }

    [Fact]
    public void ReviseEntry_InvalidInput_LeavesEntryUnchanged()
    {
        var entry = FaqEntry.Create("Vad är ränta?", "Svar.");

        Assert.Throws<ArgumentException>(() => entry.ReviseEntry("kort", "Nytt svar.", null, null));

        Assert.Equal("Vad är ränta?", entry.Question);
        Assert.Equal("Svar.", entry.Answer);
    }

    [Fact]
    public void FaqRelationship_Create_SetsAllProperties()
    {
        var relationId = Guid.NewGuid();
        var relatedId = Guid.NewGuid();

        var relationship = FaqRelationship.Create(relationId, relatedId, 2);

        Assert.Equal(relationId, relationship.RelationId);
        Assert.Equal(relatedId, relationship.RelatedRelationId);
        Assert.Equal(2, relationship.SortOrder);
    }

    [Fact]
    public void FaqSearchLog_Create_SetsAllProperties()
    {
        var searchedAt = DateTime.UtcNow;

        var log = FaqSearchLog.Create("Ränta?", "ränta", "sv", 3, new string('a', 64), searchedAt);

        Assert.Equal("Ränta?", log.Query);
        Assert.Equal("ränta", log.NormalizedQuery);
        Assert.Equal("sv", log.Language);
        Assert.Equal(3, log.ResultCount);
        Assert.Equal(new string('a', 64), log.SessionHash);
        Assert.Equal(searchedAt, log.SearchedAt);
    }

    [Fact]
    public void FaqViewLog_Create_SetsAllProperties()
    {
        var viewedAt = DateTime.UtcNow;

        var log = FaqViewLog.Create(7, new string('b', 64), viewedAt);

        Assert.Equal(7, log.FaqEntryId);
        Assert.Equal(new string('b', 64), log.SessionHash);
        Assert.Equal(viewedAt, log.ViewedAt);
    }
}
