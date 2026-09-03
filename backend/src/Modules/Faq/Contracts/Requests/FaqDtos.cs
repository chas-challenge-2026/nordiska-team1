using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Faq.Contracts.Requests;

public record CreateFaqRequest(
    [property: Required]
    [property: StringLength(500, MinimumLength = 5)]
    string Question,

    [property: Required]
    [property: StringLength(2000, MinimumLength = 1)]
    string Answer,

    [property: StringLength(200)]
    string? Category,

    [property: StringLength(500)]
    string? Keywords
);

public record UpdateFaqRequest(
    [property: Required]
    int Id,

    [property: StringLength(500, MinimumLength = 5)]
    string? Question,

    [property: StringLength(2000, MinimumLength = 1)]
    string? Answer,

    [property: StringLength(200)]
    string? Category,

    [property: StringLength(500)]
    string? Keywords
);
