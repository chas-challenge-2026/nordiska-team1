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
            req.Keywords);
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

            string.IsNullOrWhiteSpace(req.Category)
                ? target.Category
                : req.Category,

            string.IsNullOrWhiteSpace(req.Keywords)
                ? target.Keywords
                : req.Keywords);
    }

    public static FaqEntryResponse ToResponse(this FaqEntry e)
        => new(e.Id, e.Question, e.Answer, e.Category, e.HelpfulCount, e.Keywords);
}
