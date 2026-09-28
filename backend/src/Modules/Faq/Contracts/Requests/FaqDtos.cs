using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Faq.Contracts.Requests;

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
    string? Lang = "sv"
);

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

public record PatchFaqRequest(
    [property: StringLength(500, MinimumLength = 5)]
    string? Title = null,

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

public record FaqQueryParameters(
    int Page = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? Category = null,
    string? Keyword = null,
    string? Lang = null
);

