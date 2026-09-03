using FluentValidation;
using Nordiska.Modules.Banking.Contracts.Requests;

namespace Nordiska.Modules.Banking.Contracts.Validators;

public sealed class OpenSavingsAccountRequestValidator : AbstractValidator<OpenSavingsAccountRequest>
{
    public OpenSavingsAccountRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.AccountNumber).NotEmpty().Length(4, 34);
        RuleFor(x => x.AccountType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.InitialDeposit).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000_000);
        RuleFor(x => x.InterestRate).InclusiveBetween(0.0m, 100.0m);
    }
}
