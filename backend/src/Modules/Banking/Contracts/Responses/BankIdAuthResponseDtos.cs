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
    IEnumerable<string>? Errors = null,
    AuthFailureReason? FailureReason = null,
    DateTimeOffset? LockoutEnd = null
);

// Lets the controller pick the right status code (401 vs 423) without parsing the error message
public enum AuthFailureReason
{
    InvalidCredentials,
    LockedOut
}