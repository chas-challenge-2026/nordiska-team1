using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Reporting.Contracts.Requests;

public sealed record AccountStatementRequest(
    [Required]
    long AccountId,

    [Required]
    DateOnly FromDate,

    [Required]
    DateOnly ToDate);
