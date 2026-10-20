using Nordiska.Modules.Faq.Contracts.Mappers;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Tests;

public class FaqMappersTests
{
    private static FaqEntry NewEntry()
        => FaqEntry.Create("Gammal fråga här?", "Gammalt svar.", "Konto", "konto, sparande", "sv");

    [Fact]
    public void ToDomain_MapsAllFields()
    {
        var relationId = Guid.NewGuid();

        var entry = new CreateFaqRequest("Vad är ränta?", "Ersättning.", "Ränta", "ränta", "en", relationId).ToDomain();

        Assert.Equal("Vad är ränta?", entry.Question);
        Assert.Equal("Ersättning.", entry.Answer);
        Assert.Equal("Ränta", entry.Category);
        Assert.Equal("ränta", entry.Keywords);
        Assert.Equal("en", entry.Language);
        Assert.Equal(relationId, entry.RelationId);
    }

    [Fact]
    public void ToDomain_MissingLang_DefaultsToSwedish()
    {
        var entry = new CreateFaqRequest("Vad är ränta?", "Svar.", Lang: null).ToDomain();

        Assert.Equal("sv", entry.Language);
    }

    [Fact]
    public void ApplyUpdate_ReplacesProvidedFields()
    {
        var entry = NewEntry();

        entry.ApplyUpdate(new UpdateFaqRequest(1, "Ny fråga här?", "Nytt svar.", "Ränta", "ränta", "en"));

        Assert.Equal("Ny fråga här?", entry.Question);
        Assert.Equal("Nytt svar.", entry.Answer);
        Assert.Equal("Ränta", entry.Category);
        Assert.Equal("ränta", entry.Keywords);
        Assert.Equal("en", entry.Language);
    }

    [Fact]
    public void ApplyUpdate_BlankOrMissingFields_KeepExistingValues()
    {
        var entry = NewEntry();

        entry.ApplyUpdate(new UpdateFaqRequest(1, Question: "  ", Answer: ""));

        Assert.Equal("Gammal fråga här?", entry.Question);
        Assert.Equal("Gammalt svar.", entry.Answer);
        Assert.Equal("Konto", entry.Category);
        Assert.Equal("konto, sparande", entry.Keywords);
        Assert.Equal("sv", entry.Language);
    }

    [Fact]
    public void ApplyPatch_QuestionWinsOverTitle()
    {
        var entry = NewEntry();

        entry.ApplyPatch(new PatchFaqRequest(Title: "Titel som tappar", Question: "Fråga som vinner?"));

        Assert.Equal("Fråga som vinner?", entry.Question);
    }

    [Fact]
    public void ApplyPatch_UsesTitleWhenQuestionIsMissing()
    {
        var entry = NewEntry();

        entry.ApplyPatch(new PatchFaqRequest(Title: "Ny titel här"));

        Assert.Equal("Ny titel här", entry.Question);
        Assert.Equal("Gammalt svar.", entry.Answer);
    }

    [Fact]
    public void ApplyPatch_EmptyPatch_KeepsEverything()
    {
        var entry = NewEntry();

        entry.ApplyPatch(new PatchFaqRequest());

        Assert.Equal("Gammal fråga här?", entry.Question);
        Assert.Equal("Gammalt svar.", entry.Answer);
        Assert.Equal("Konto", entry.Category);
        Assert.Equal("konto, sparande", entry.Keywords);
    }

    [Fact]
    public void ApplyPatch_ReplacesAnswerCategoryKeywordsAndLanguage()
    {
        var entry = NewEntry();

        entry.ApplyPatch(new PatchFaqRequest(Answer: "Nytt svar.", Category: "Kort", Keywords: "kort", Lang: "en"));

        Assert.Equal("Nytt svar.", entry.Answer);
        Assert.Equal("Kort", entry.Category);
        Assert.Equal("kort", entry.Keywords);
        Assert.Equal("en", entry.Language);
    }

    [Fact]
    public void ToResponse_MapsEntryFields()
    {
        var relationId = Guid.NewGuid();
        var entry = FaqEntry.Create("Vad är ränta?", "Svar.", "Ränta", "ränta", "sv", relationId);
        entry.MarkHelpful();

        var response = entry.ToResponse();

        Assert.Equal("Vad är ränta?", response.Question);
        Assert.Equal("Vad är ränta?", response.Title);
        Assert.Equal("Svar.", response.Answer);
        Assert.Equal("Ränta", response.Category);
        Assert.Equal(1, response.HelpfulCount);
        Assert.Equal(relationId, response.RelationId);
        Assert.Equal("sv", response.Lang);
        Assert.Equal(entry.CreatedAt, response.CreatedAt);
        Assert.Equal(entry.UpdatedAt, response.UpdatedAt);
    }

    [Fact]
    public void ToResponse_SplitsKeywordsOnCommaAndSemicolonAndTrims()
    {
        var entry = FaqEntry.Create("Vad är ränta?", "Svar.", "Ränta", "ränta; pengar, sparande ,", "sv");

        Assert.Equal(new[] { "ränta", "pengar", "sparande" }, entry.ToResponse().Keywords);
    }

    [Fact]
    public void ToResponse_EmptyKeywords_ReturnsEmptyList()
    {
        var entry = FaqEntry.Create("Vad är ränta?", "Svar.");

        Assert.Empty(entry.ToResponse().Keywords);
    }
}
