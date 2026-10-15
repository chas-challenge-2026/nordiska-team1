using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Contracts.Responses;
using Nordiska.Modules.Faq.Contracts.Validators;

namespace Nordiska.Modules.Faq.Tests;

public class FaqContractTests
{
    private readonly CreateFaqRequestValidator _create = new();
    private readonly UpdateFaqRequestValidator _update = new();

    [Fact]
    public void CreateValidator_ValidRequest_IsValid()
    {
        Assert.True(_create.Validate(new CreateFaqRequest("Vad är ränta?", "Svar.", "Ränta", "ränta")).IsValid);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void CreateValidator_QuestionLength(int length, bool valid)
    {
        var request = new CreateFaqRequest(new string('a', length), "Svar.");

        Assert.Equal(valid, _create.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void CreateValidator_AnswerLength(int length, bool valid)
    {
        var request = new CreateFaqRequest("Vad är ränta?", new string('a', length));

        Assert.Equal(valid, _create.Validate(request).IsValid);
    }

    [Fact]
    public void CreateValidator_CategoryAndKeywordsTooLong_IsInvalid()
    {
        Assert.False(_create.Validate(new CreateFaqRequest("Vad är ränta?", "Svar.", new string('a', 201))).IsValid);
        Assert.False(_create.Validate(new CreateFaqRequest("Vad är ränta?", "Svar.", null, new string('a', 501))).IsValid);
    }

    [Fact]
    public void UpdateValidator_OnlyId_IsValid()
    {
        Assert.True(_update.Validate(new UpdateFaqRequest(1)).IsValid);
    }

    [Fact]
    public void UpdateValidator_IdMustBePositive()
    {
        Assert.False(_update.Validate(new UpdateFaqRequest(0)).IsValid);
    }

    [Fact]
    public void UpdateValidator_ProvidedFieldsAreLengthChecked()
    {
        Assert.False(_update.Validate(new UpdateFaqRequest(1, Question: "abcd")).IsValid);
        Assert.False(_update.Validate(new UpdateFaqRequest(1, Answer: "")).IsValid);
        Assert.False(_update.Validate(new UpdateFaqRequest(1, Category: new string('a', 201))).IsValid);
        Assert.False(_update.Validate(new UpdateFaqRequest(1, Keywords: new string('a', 501))).IsValid);
        Assert.True(_update.Validate(new UpdateFaqRequest(1, "Vad är ränta?", "Svar.", "Ränta", "ränta", "sv")).IsValid);
    }

    [Fact]
    public void RequestRecords_SupportCopyAndValueEquality()
    {
        var create = new CreateFaqRequest("Vad är ränta?", "Svar.", "Ränta", "ränta", "sv");
        var update = new UpdateFaqRequest(1, "Vad är ränta?", "Svar.", "Ränta", "ränta", "sv");
        var patch = new PatchFaqRequest("Titel", "Fråga här?", "Svar.", "Ränta", "ränta", "sv");
        var search = new SearchFaqRequest("ränta", "Ränta", "ränta", "sv");
        var paging = new FaqQueryParameters(2, 10, "ränta", "Ränta", "ränta", "sv");

        Assert.Equal(create, create with { });
        Assert.NotEqual(create, create with { Answer = "Annat" });
        Assert.Equal(update, update with { });
        Assert.NotEqual(update, update with { Id = 2 });
        Assert.Equal(patch, patch with { });
        Assert.NotEqual(patch, patch with { Title = "Annan" });
        Assert.Equal(search, search with { });
        Assert.Equal(paging, paging with { });

        Assert.Equal(create.GetHashCode(), (create with { }).GetHashCode());
        Assert.Equal(update.GetHashCode(), (update with { }).GetHashCode());
        Assert.Equal(patch.GetHashCode(), (patch with { }).GetHashCode());
        Assert.Contains("Vad är ränta?", create.ToString());
        Assert.Contains("Vad är ränta?", update.ToString());
        Assert.Contains("Titel", patch.ToString());
    }

    [Fact]
    public void SetRelatedRequest_HoldsRelationIds()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var request = new SetRelatedFaqsRequest(ids);

        Assert.Same(ids, request.RelatedRelationIds);
        Assert.Equal(request, request with { });
        Assert.Contains(nameof(SetRelatedFaqsRequest.RelatedRelationIds), request.ToString());
    }

    [Fact]
    public void ResponseRecords_SupportCopyAndValueEquality()
    {
        var searchedAt = DateTime.UtcNow;
        var gap = new FaqContentGapResponse("ränta", "sv", 4, searchedAt);
        var related = new RelatedFaqResponse(1, "Vad är ränta?", "Ränta");
        var created = new FaqCreatedResponse(9);
        var keywords = new[] { "ränta" };
        var entry = new FaqEntryResponse(1, "Vad är ränta?", "Svar.", "Ränta", 2, keywords, Guid.NewGuid());

        Assert.Equal(gap, gap with { });
        Assert.NotEqual(gap, gap with { SearchCount = 5 });
        Assert.Equal(related, related with { });
        Assert.NotEqual(related, related with { Id = 2 });
        Assert.Equal(created, created with { });
        Assert.Equal(entry, entry with { });
        Assert.NotEqual(entry, entry with { HelpfulCount = 3 });

        Assert.Equal(gap.GetHashCode(), (gap with { }).GetHashCode());
        Assert.Equal(related.GetHashCode(), (related with { }).GetHashCode());
        Assert.Equal(created.GetHashCode(), (created with { }).GetHashCode());
        Assert.Contains("ränta", gap.ToString());
        Assert.Contains("Ränta", related.ToString());
        Assert.Contains("9", created.ToString());
        Assert.Equal("Vad är ränta?", entry.Title);
        Assert.Null(entry.RelatedFaqs);
        Assert.Contains("Vad är ränta?", entry.ToString());
    }
}
