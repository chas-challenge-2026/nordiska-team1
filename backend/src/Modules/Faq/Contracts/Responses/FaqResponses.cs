namespace Nordiska.Modules.Faq.Contracts.Responses;

public record FaqEntryResponse(
    int Id,
    string Question,
    string Answer,
    string Category,
    int HelpfulCount,
    string Keywords
);
