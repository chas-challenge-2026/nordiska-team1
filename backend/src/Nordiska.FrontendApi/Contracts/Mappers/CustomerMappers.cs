using Nordiska.Modules.Banking.Domain;
using Nordiska.FrontendApi.Contracts.Requests;

namespace Nordiska.FrontendApi.Contracts.Mappers;

public static class CustomerMappers
{
    public static void ApplyUpdate(this Customer target, UpdateCustomerRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.Name)) target.Name = req.Name!;
        if (!string.IsNullOrWhiteSpace(req.Email)) target.Email = req.Email!;
        if (!string.IsNullOrWhiteSpace(req.PersonalNum)) target.PersonalNum = req.PersonalNum!;
    }

    public static CustomerResponse ToResponse(this Customer c)
        => new(c.Id, c.PersonalNum, c.Name, c.Email, c.CreatedAt);
}
