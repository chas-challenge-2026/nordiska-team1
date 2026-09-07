using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Banking.Contracts.Requests;

public record TransactionRequest(
    [property: Required]
    long AccountId,

    [property: Required]
    [property: StringLength(50)]
    string Type,

    [property: Range(0.01, 1_000_000_000)]
    decimal Amount
);
