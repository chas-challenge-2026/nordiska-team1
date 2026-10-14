using FluentValidation;
using Nordiska.Modules.Inbox.Contracts.Requests;

namespace Nordiska.Modules.Inbox.Contracts.Validators;

public sealed class CreateAdminThreadRequestValidator : AbstractValidator<CreateAdminThreadRequest>
{
    public CreateAdminThreadRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Ämne är obligatoriskt.")
            .MaximumLength(300).WithMessage("Ämnet får inte överstiga 300 tecken.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Meddelande är obligatoriskt.")
            .MaximumLength(10000).WithMessage("Meddelandet får inte överstiga 10 000 tecken.");

        When(x => !x.BroadcastToAll, () =>
        {
            RuleFor(x => x.CustomerId)
                .NotNull().WithMessage("Kund-ID är obligatoriskt när meddelandet inte är ett allmänt utskick.")
                .GreaterThan(0).WithMessage("Kund-ID måste vara ett giltigt ID.");
        });
    }
}
