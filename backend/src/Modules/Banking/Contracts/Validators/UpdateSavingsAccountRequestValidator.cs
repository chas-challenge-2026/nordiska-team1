using FluentValidation;
using Nordiska.Modules.Banking.Contracts.Requests;

namespace Nordiska.Modules.Banking.Contracts.Validators;

public sealed class UpdateSavingsAccountRequestValidator : AbstractValidator<UpdateSavingsAccountRequest>
{
    public UpdateSavingsAccountRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        When(x => x.AccountType is not null, () => RuleFor(x => x.AccountType).NotEmpty().MaximumLength(100));
        When(x => x.Balance.HasValue, () => RuleFor(x => x.Balance).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000_000));
        When(x => x.InterestRate.HasValue, () => RuleFor(x => x.InterestRate).InclusiveBetween(0.0m, 100.0m));
    }
}
