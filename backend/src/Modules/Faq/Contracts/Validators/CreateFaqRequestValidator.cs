using FluentValidation;
using Nordiska.Modules.Faq.Contracts.Requests;

namespace Nordiska.Modules.Faq.Contracts.Validators;

public sealed class CreateFaqRequestValidator : AbstractValidator<CreateFaqRequest>
{
    public CreateFaqRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().Length(5, 500);
        RuleFor(x => x.Answer).NotEmpty().Length(1, 2000);
        When(x => x.Category is not null, () => RuleFor(x => x.Category).MaximumLength(200));
        When(x => x.Keywords is not null, () => RuleFor(x => x.Keywords).MaximumLength(500));
    }
}
