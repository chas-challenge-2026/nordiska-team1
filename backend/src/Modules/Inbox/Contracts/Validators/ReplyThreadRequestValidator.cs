using FluentValidation;
using Nordiska.Modules.Inbox.Contracts.Requests;

namespace Nordiska.Modules.Inbox.Contracts.Validators;

public sealed class ReplyThreadRequestValidator : AbstractValidator<ReplyThreadRequest>
{
    public ReplyThreadRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Meddelande är obligatoriskt.")
            .MaximumLength(10000).WithMessage("Meddelandet får inte överstiga 10 000 tecken.");
    }
}
