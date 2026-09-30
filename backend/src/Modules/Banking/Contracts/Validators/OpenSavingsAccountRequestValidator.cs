using FluentValidation;
using Nordiska.Modules.Banking.Contracts.Requests;

namespace Nordiska.Modules.Banking.Contracts.Validators;

public sealed class OpenSavingsAccountRequestValidator : AbstractValidator<OpenSavingsAccountRequest>
{
    public OpenSavingsAccountRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        When(x => !string.IsNullOrWhiteSpace(x.AccountNumber), () => RuleFor(x => x.AccountNumber!).Length(4, 34));
        RuleFor(x => x.AccountType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.InitialDeposit).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000_000);
        When(x => x.InterestRate.HasValue, () => RuleFor(x => x.InterestRate!.Value).InclusiveBetween(0.0m, 100.0m));
    }
}
