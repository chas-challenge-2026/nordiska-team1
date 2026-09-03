using FluentValidation;
using Nordiska.FrontendApi.Contracts.Requests;

namespace Nordiska.FrontendApi.Contracts.Validators;

public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        When(x => x.Name is not null, () => RuleFor(x => x.Name).NotEmpty().MaximumLength(200));
        When(x => x.Email is not null, () => RuleFor(x => x.Email).EmailAddress().MaximumLength(320));
        When(x => x.PersonalNum is not null, () => RuleFor(x => x.PersonalNum).Matches("^[0-9A-Za-z-]{10,12}$").WithMessage("PersonalNum must be 10-12 chars"));
    }
}
