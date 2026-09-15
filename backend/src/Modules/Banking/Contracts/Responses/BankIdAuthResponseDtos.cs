namespace Nordiska.FrontendApi.Contracts.Responses;

public record BankIdInitiateResponseDto(
    string OrderRef,
    string AutoStartToken,
    string QrStartToken,
    string QrStartSecret
);

public record BankIdCollectResponseDto(
    string Status,
    string? HintCode,
    CustomerResponseDto? Customer
);

public record CustomerResponseDto(
    long Id,
    string Email,
    string Name
);

public record AuthenticationResultDto(
    bool IsSuccess,
    string? ErrorMessage,
    string? Token = null,
    BankIdInitiateResponseDto? InitiateData = null,
    BankIdCollectResponseDto? CollectData = null,
    IEnumerable<string>? Errors = null
);