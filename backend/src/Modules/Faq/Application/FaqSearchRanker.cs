using System.Text.RegularExpressions;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.Modules.Faq.Application;

public sealed record FaqRankedMatch(FaqEntryResponse Entry, int Score);

public static class FaqSearchRanker
{
    private const int ExactQuestionBonus = 10;

    private static readonly Regex TokenSeparators = new(
        @"[\p{P}\p{S}\s]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<FaqRankedMatch> Rank(
        IEnumerable<FaqEntryResponse> candidates,
        string query)
    {
        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0)
        {
            return Array.Empty<FaqRankedMatch>();
        }

        var queryWords = ToWords(normalizedQuery);
        var matches = new List<FaqRankedMatch>();

        foreach (var entry in candidates)
        {
            var questionWords = ToWordSet(entry.Question);
            var keywordWords = ToWordSet(string.Join(" ", entry.Keywords));
            var categoryWords = ToWordSet(entry.Category);
            var answerWords = ToWordSet(entry.Answer);

            var score = 0;

            foreach (var word in queryWords)
            {
                if (questionWords.Contains(word))
                {
                    score += 5;
                }
                else if (keywordWords.Contains(word))
                {
                    score += 4;
                }
                else if (categoryWords.Contains(word))
                {
                    score += 3;
                }
                else if (answerWords.Contains(word))
                {
                    score += 1;
                }
            }

            if (score == 0)
            {
                continue;
            }

            if (Normalize(entry.Question) == normalizedQuery)
            {
                score += ExactQuestionBonus;
            }

            matches.Add(new FaqRankedMatch(entry, score));
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Entry.HelpfulCount)
            .ThenBy(match => match.Entry.Id)
            .ToList();
    }

    private static HashSet<string> ToWordSet(string value)
        => new(ToWords(Normalize(value)), StringComparer.Ordinal);

    private static string[] ToWords(string normalizedText)
        => normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            TokenSeparators
                .Split(value.Trim().ToLowerInvariant())
                .Where(token => token.Length > 0));
    }
}