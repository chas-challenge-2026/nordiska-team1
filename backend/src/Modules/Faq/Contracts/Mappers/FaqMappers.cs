using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.Modules.Faq.Contracts.Mappers;

public static class FaqMappers
{
    public static FaqEntry ToDomain(this CreateFaqRequest req)
        => new()
        {
            Question = req.Question,
            Answer = req.Answer,
            Category = req.Category ?? string.Empty,
            Keywords = req.Keywords ?? string.Empty,
            HelpfulCount = 0
        };

    public static void ApplyUpdate(this FaqEntry target, UpdateFaqRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.Question)) target.Question = req.Question!;
        if (!string.IsNullOrWhiteSpace(req.Answer)) target.Answer = req.Answer!;
        if (!string.IsNullOrWhiteSpace(req.Category)) target.Category = req.Category!;
        if (!string.IsNullOrWhiteSpace(req.Keywords)) target.Keywords = req.Keywords!;
    }

    public static FaqEntryResponse ToResponse(this FaqEntry e)
        => new(e.Id, e.Question, e.Answer, e.Category, e.HelpfulCount, e.Keywords);
}
