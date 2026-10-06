using FluentValidation;
using Nordiska.Modules.Inbox.Contracts.Requests;

namespace Nordiska.Modules.Inbox.Contracts.Validators;

public sealed class CreateThreadRequestValidator : AbstractValidator<CreateThreadRequest>
{
    public CreateThreadRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Ämne är obligatoriskt.")
            .MaximumLength(300).WithMessage("Ämnet får inte överstiga 300 tecken.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Meddelande är obligatoriskt.")
            .MaximumLength(10000).WithMessage("Meddelandet får inte överstiga 10 000 tecken.");

        When(x => !string.IsNullOrWhiteSpace(x.Category), () =>
        {
            RuleFor(x => x.Category)
                .Must(c => c is null ||
                           c.Equals("Sparkonto", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Savings", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("SavingsAccount", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Sparmål", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Sparmal", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Goals", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("SavingsGoals", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Konto", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Account", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Transaktioner", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Transactions", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Skatteunderlag", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Tax", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("TaxReport", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Allmänt", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Allmant", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("General", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Kategori måste vara någon av: Sparkonto, Sparmål, Skatteunderlag, Allmänt (eller motsvarande på engelska).");
        });
    }
}
