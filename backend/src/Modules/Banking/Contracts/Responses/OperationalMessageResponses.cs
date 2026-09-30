using System;

namespace Nordiska.Modules.Banking.Contracts.Responses;

/// <summary>
/// Bilingual text content holding Swedish and English variants.
/// </summary>
/// <param name="Sv">Swedish text.</param>
/// <param name="En">English text.</param>
public record BilingualText(string Sv, string En);

/// <summary>
/// Response model for an operational message / banner.
/// </summary>
public record OperationalMessageResponse(
    long Id,
    BilingualText Title,
    BilingualText Message,
    string Severity,
    bool IsActive,
    int Priority,
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
