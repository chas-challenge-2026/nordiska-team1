using ActiveLogin.Identity.Swedish;
using FluentValidation;
using Nordiska.FrontendApi.Contracts.Requests;

namespace Nordiska.FrontendApi.Contracts.Validators;

public sealed class RegisterCustomerRequestDtoValidator : AbstractValidator<RegisterCustomerRequestDto>
{
    public RegisterCustomerRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Namn är obligatoriskt.")
            .MaximumLength(200).WithMessage("Namnet får inte vara längre än 200 tecken.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-post är obligatoriskt.")
            .EmailAddress().WithMessage("Ogiltig e-postadress.")
            .MaximumLength(320).WithMessage("E-postadressen får inte vara längre än 320 tecken.");

        RuleFor(x => x.PersonalNum)
            .NotEmpty().WithMessage("Personnummer är obligatoriskt.")
            .Custom((personalNum, context) =>
            {
                if (string.IsNullOrWhiteSpace(personalNum))
                {
                    return;
                }

                if (!PersonalIdentityNumber.TryParse(personalNum, out var pin))
                {
                    context.AddFailure("PersonalNum", "Ogiltigt personnummer (kontrollsiffran eller formatet stämmer inte).");
                    return;
                }

                var birthDate = new DateTime(pin.Year, pin.Month, pin.Day, 0, 0, 0, DateTimeKind.Utc);
                var today = DateTime.UtcNow.Date;
                var age = today.Year - birthDate.Year;
                if (birthDate > today.AddYears(-age))
                {
                    age--;
                }

                if (age < 18)
                {
                    context.AddFailure("PersonalNum", "Du måste vara minst 18 år för att bli kund.");
                }
            });

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50).WithMessage("Telefonnumret får inte vara längre än 50 tecken.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}
