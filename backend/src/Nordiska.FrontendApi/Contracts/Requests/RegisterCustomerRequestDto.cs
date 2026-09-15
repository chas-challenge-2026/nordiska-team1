namespace Nordiska.FrontendApi.Contracts.Requests;

public record RegisterCustomerRequestDto(
    string Name,
    string Email,
    string PersonalNum,
    string PhoneNumber
    );