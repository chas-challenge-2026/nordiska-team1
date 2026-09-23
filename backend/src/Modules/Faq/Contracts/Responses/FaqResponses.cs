namespace Nordiska.Modules.Faq.Contracts.Responses;

public record FaqEntryResponse(
    int Id,
    string Question,
    string Answer,
    string Category,
    int HelpfulCount,
    IReadOnlyList<string> Keywords,
    string Lang = "sv",
    DateTime? CreatedAt = null,
    DateTime? UpdatedAt = null)
{
    public string Title => Question;
}

public sealed record FaqCreatedResponse(int Id);

