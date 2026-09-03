using FluentValidation;
using Nordiska.Modules.Banking.Contracts.Requests;

namespace Nordiska.Modules.Banking.Contracts.Validators;

public sealed class CloseSavingsAccountRequestValidator : AbstractValidator<CloseSavingsAccountRequest>
{
    public CloseSavingsAccountRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
