using System;
using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Banking.Contracts.Requests;

/// <summary>
/// Request payload for creating a new bilingual operational message.
/// </summary>
public record CreateOperationalMessageRequest(
    [Required, StringLength(200, MinimumLength = 1)] string TitleSv,
    [Required, StringLength(200, MinimumLength = 1)] string TitleEn,
    [Required, StringLength(1000, MinimumLength = 1)] string MessageSv,
    [Required, StringLength(1000, MinimumLength = 1)] string MessageEn,
    [RegularExpression("^(info|warning|critical)$", ErrorMessage = "Severity must be 'info', 'warning', or 'critical'")] string Severity = "info",
    bool IsActive = true,
    int Priority = 0,
    DateTime? StartDate = null,
    DateTime? EndDate = null
);

/// <summary>
/// Request payload for replacing an existing operational message.
/// </summary>
public record UpdateOperationalMessageRequest(
    [Required, StringLength(200, MinimumLength = 1)] string TitleSv,
    [Required, StringLength(200, MinimumLength = 1)] string TitleEn,
    [Required, StringLength(1000, MinimumLength = 1)] string MessageSv,
    [Required, StringLength(1000, MinimumLength = 1)] string MessageEn,
    [RegularExpression("^(info|warning|critical)$", ErrorMessage = "Severity must be 'info', 'warning', or 'critical'")] string Severity = "info",
    bool IsActive = true,
    int Priority = 0,
    DateTime? StartDate = null,
    DateTime? EndDate = null
);

/// <summary>
/// Request payload for partially updating an operational message (e.g. toggling active status).
/// </summary>
public record PatchOperationalMessageRequest(
    [StringLength(200, MinimumLength = 1)] string? TitleSv = null,
    [StringLength(200, MinimumLength = 1)] string? TitleEn = null,
    [StringLength(1000, MinimumLength = 1)] string? MessageSv = null,
    [StringLength(1000, MinimumLength = 1)] string? MessageEn = null,
    [RegularExpression("^(info|warning|critical)$", ErrorMessage = "Severity must be 'info', 'warning', or 'critical'")] string? Severity = null,
    bool? IsActive = null,
    int? Priority = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
);
