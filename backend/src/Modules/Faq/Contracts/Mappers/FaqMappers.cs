using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.Modules.Faq.Contracts.Mappers;

public static class FaqMappers
{
    public static FaqEntry ToDomain(this CreateFaqRequest req)
    {
        return FaqEntry.Create(
            req.Question,
            req.Answer,
            req.Category,
            req.Keywords,
            req.Lang ?? "sv");
    }

    public static void ApplyUpdate(this FaqEntry target, UpdateFaqRequest req)
    {
        target.ReviseEntry(
            string.IsNullOrWhiteSpace(req.Question)
                ? target.Question
                : req.Question,

            string.IsNullOrWhiteSpace(req.Answer)
                ? target.Answer
                : req.Answer,

            req.Category ?? target.Category,
            req.Keywords ?? target.Keywords,
            req.Lang ?? target.Language);
    }

    public static void ApplyPatch(this FaqEntry target, PatchFaqRequest req)
    {
        var newQuestion = !string.IsNullOrWhiteSpace(req.Question)
            ? req.Question
            : (!string.IsNullOrWhiteSpace(req.Title) ? req.Title : target.Question);

        var newAnswer = !string.IsNullOrWhiteSpace(req.Answer) ? req.Answer : target.Answer;
        var newCategory = req.Category ?? target.Category;
        var newKeywords = req.Keywords ?? target.Keywords;
        var newLang = req.Lang ?? target.Language;

        target.ReviseEntry(newQuestion, newAnswer, newCategory, newKeywords, newLang);
    }

    public static FaqEntryResponse ToResponse(this FaqEntry e)
    {
        var keywordsList = string.IsNullOrWhiteSpace(e.Keywords)
            ? Array.Empty<string>()
            : e.Keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new FaqEntryResponse(
            e.Id,
            e.Question,
            e.Answer,
            e.Category,
            e.HelpfulCount,
            keywordsList,
            e.Language,
            e.CreatedAt,
            e.UpdatedAt);
    }
}

