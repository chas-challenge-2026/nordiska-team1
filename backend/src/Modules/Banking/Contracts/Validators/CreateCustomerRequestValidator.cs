using FluentValidation;
using Nordiska.Modules.Banking.Contracts.Requests;

namespace Nordiska.Modules.Banking.Contracts.Validators;

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.PersonalNum).NotEmpty().Matches("^[0-9A-Za-z-]{10,12}$").WithMessage("PersonalNum must be 10-12 chars");
    }
}
