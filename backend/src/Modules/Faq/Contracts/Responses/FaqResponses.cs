using System.Text.Json.Serialization;

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

    // Only filled in when a single article is fetched, left out of list responses
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RelatedFaqResponse>? RelatedFaqs { get; init; }
}

public sealed record RelatedFaqResponse(
    int Id,
    string Question,
    string Category);

public sealed record FaqCreatedResponse(int Id);

public sealed record FaqContentGapResponse(
    string Query,
    string Lang,
    int SearchCount,
    DateTime LastSearchedAt);

