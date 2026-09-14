namespace Nordiska.Modules.Banking.Contracts.Requests;

public record CreateCustomerRequest(
    string Name,
    string Email,
    string PersonalNum
);
