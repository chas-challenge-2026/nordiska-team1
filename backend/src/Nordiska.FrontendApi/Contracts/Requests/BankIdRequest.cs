namespace Nordiska.FrontendApi.Contracts.Requests;

public record BankIdInitiateRequest(string PersonalNum);
public record BankIdCollectRequest(string OrderRef);