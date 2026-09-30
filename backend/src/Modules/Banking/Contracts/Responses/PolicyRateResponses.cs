namespace Nordiska.Modules.Banking.Contracts.Responses;

public record PolicyRateResponse(
    decimal Rate,
    DateOnly EffectiveDate,
    string Source,
    bool Stale
);
