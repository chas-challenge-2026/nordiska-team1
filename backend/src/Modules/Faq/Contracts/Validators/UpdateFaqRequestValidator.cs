using FluentValidation;
using Nordiska.Modules.Faq.Contracts.Requests;

namespace Nordiska.Modules.Faq.Contracts.Validators;

public sealed class UpdateFaqRequestValidator : AbstractValidator<UpdateFaqRequest>
{
    public UpdateFaqRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        When(x => x.Question is not null, () => RuleFor(x => x.Question).Length(5, 500));
        When(x => x.Answer is not null, () => RuleFor(x => x.Answer).Length(1, 2000));
        When(x => x.Category is not null, () => RuleFor(x => x.Category).MaximumLength(200));
        When(x => x.Keywords is not null, () => RuleFor(x => x.Keywords).MaximumLength(500));
    }
}
