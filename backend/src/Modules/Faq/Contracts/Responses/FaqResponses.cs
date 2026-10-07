namespace Nordiska.Modules.Faq.Contracts.Responses;

public record FaqEntryResponse(
    int Id,
    string Question,
    string Answer,
    string Category,
    int HelpfulCount,
    IReadOnlyList<string> Keywords,
    Guid RelationId,
    string Lang = "sv",
    DateTime? CreatedAt = null,
    DateTime? UpdatedAt = null)
{
    public string Title => Question;
}

public sealed record FaqCreatedResponse(int Id);

public sealed record FaqContentGapResponse(
    string Query,
    string Lang,
    int SearchCount,
    DateTime LastSearchedAt);

