using FluentValidation;
using Nordiska.Modules.Banking.Contracts.Requests;

namespace Nordiska.Modules.Banking.Contracts.Validators;

public sealed class CreateAccountTypeConfigRequestValidator : AbstractValidator<CreateAccountTypeConfigRequest>
{
    public CreateAccountTypeConfigRequestValidator()
    {
        RuleFor(x => x.AccountType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.InterestRate).InclusiveBetween(0.0m, 100.0m);
        When(x => x.Description is not null, () => RuleFor(x => x.Description).MaximumLength(1000));
    }
}

public sealed class UpdateAccountTypeConfigRequestValidator : AbstractValidator<UpdateAccountTypeConfigRequest>
{
    public UpdateAccountTypeConfigRequestValidator()
    {
        RuleFor(x => x.AccountType).NotEmpty().MaximumLength(100);
        When(x => x.InterestRate.HasValue, () => RuleFor(x => x.InterestRate).InclusiveBetween(0.0m, 100.0m));
        When(x => x.Description is not null, () => RuleFor(x => x.Description).MaximumLength(1000));
    }
}
