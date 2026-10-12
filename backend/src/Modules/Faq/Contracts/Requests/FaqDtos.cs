using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Faq.Contracts.Requests;

/// <summary>
/// Payload for creating a new FAQ entry. Requires the faq:manage policy (Admin role).
/// </summary>
/// <param name="Question">The FAQ question text (5 to 500 characters).</param>
/// <param name="Answer">The detailed answer text (1 to 2000 characters).</param>
/// <param name="Category">Optional category grouping (e.g. 'Konto', 'Ränta', 'BankID', max 200 characters).</param>
/// <param name="Keywords">Optional comma-separated search keywords / tags (max 500 characters).</param>
/// <param name="Lang">Language code for the entry (e.g. 'sv' or 'en', default 'sv').</param>
public record CreateFaqRequest(
    [Required]
    [StringLength(500, MinimumLength = 5)]
    string Question,

    [Required]
    [StringLength(2000, MinimumLength = 1)]
    string Answer,

    [StringLength(200)]
    string? Category = null,

    [StringLength(500)]
    string? Keywords = null,

    [StringLength(10)]
    string? Lang = "sv",

    Guid? RelationId = null
);

/// <summary>
/// Payload for fully updating an existing FAQ entry.
/// </summary>
public record UpdateFaqRequest(
    [property: Required]
    int Id,

    [property: StringLength(500, MinimumLength = 5)]
    string? Question = null,

    [property: StringLength(2000, MinimumLength = 1)]
    string? Answer = null,

    [property: StringLength(200)]
    string? Category = null,

    [property: StringLength(500)]
    string? Keywords = null,

    [property: StringLength(10)]
    string? Lang = null
);

/// <summary>
/// Payload for partially updating an FAQ entry (PATCH).
/// </summary>
public record PatchFaqRequest(
    [param: StringLength(500, MinimumLength = 5)]
    string? Title = null,

    [param: StringLength(500, MinimumLength = 5)]
    string? Question = null,

    [param: StringLength(2000, MinimumLength = 1)]
    string? Answer = null,

    [param: StringLength(200)]
    string? Category = null,

    [param: StringLength(500)]
    string? Keywords = null,

    [param: StringLength(10)]
    string? Lang = null
);

/// <summary>
/// Query payload for searching FAQ entries.
/// </summary>
public record SearchFaqRequest(
    [param: StringLength(500)]
    string? SearchTerm = null,
    [param: StringLength(200)]
    string? Category = null,
    [param: StringLength(200)]
    string? Keyword = null,
    [param: StringLength(10)]
    string? Lang = null
);

/// <summary>
/// Pagination and query parameters for FAQ listings.
/// </summary>
public record FaqQueryParameters(
    int Page = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? Category = null,
    string? Keyword = null,
    string? Lang = null
);

/// <summary>
/// Payload for setting the related articles of an FAQ article. Replaces the current list.
/// </summary>
/// <param name="RelatedRelationIds">RelationIds of the related articles, in the order they should be shown (max 5).</param>
public record SetRelatedFaqsRequest(
    [Required]
    Guid[] RelatedRelationIds
);
