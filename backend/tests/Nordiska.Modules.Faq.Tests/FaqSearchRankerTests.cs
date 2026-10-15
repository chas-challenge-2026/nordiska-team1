using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.Modules.Faq.Tests;

public class FaqSearchRankerTests
{
    [Fact]
    public void Rank_ScoresQuestionKeywordCategoryAndAnswerMatches()
    {
        var candidates = new[]
        {
            Entry(1, "alpha question", "", "", Array.Empty<string>()),
            Entry(2, "unrelated question", "", "", new[] { "alpha" }),
            Entry(3, "unrelated question", "", "alpha", Array.Empty<string>()),
            Entry(4, "unrelated question", "alpha", "", Array.Empty<string>())
        };

        var ranked = FaqSearchRanker.Rank(candidates, "alpha");

        Assert.Equal(new[] { 1, 2, 3, 4 }, ranked.Select(match => match.Entry.Id));
        Assert.Equal(new[] { 5, 4, 3, 1 }, ranked.Select(match => match.Score));
    }

    [Fact]
    public void Rank_NormalizesCaseAndPunctuation()
    {
        var entry = Entry(1, "Hur får jag ränta?");

        var ranked = FaqSearchRanker.Rank(new[] { entry }, "HUR FÅR JAG RÄNTA!!!");

        var match = Assert.Single(ranked);
        Assert.Equal(1, match.Entry.Id);
        Assert.Equal(30, match.Score); // Four question-word matches plus exact-question bonus.
    }

    [Fact]
    public void Rank_ReturnsNoResultsForUnmatchedOrEmptyQuery()
    {
        var candidates = new[] { Entry(1, "Hur får jag ränta?") };

        Assert.Empty(FaqSearchRanker.Rank(candidates, "mortgage"));
        Assert.Empty(FaqSearchRanker.Rank(candidates, "   "));
    }

    [Fact]
    public void Rank_UsesHelpfulCountThenIdToBreakTies()
    {
        var candidates = new[]
        {
            Entry(3, "alpha question", helpfulCount: 2),
            Entry(2, "alpha question", helpfulCount: 8),
            Entry(1, "alpha question", helpfulCount: 8)
        };

        var ranked = FaqSearchRanker.Rank(candidates, "alpha");

        Assert.Equal(new[] { 1, 2, 3 }, ranked.Select(match => match.Entry.Id));
    }

    private static FaqEntryResponse Entry(
        int id,
        string question,
        string answer = "",
        string category = "",
        IReadOnlyList<string>? keywords = null,
        int helpfulCount = 0)
        => new(
            id,
            question,
            answer,
            category,
            helpfulCount,
            keywords ?? Array.Empty<string>(),
            Guid.NewGuid());
}