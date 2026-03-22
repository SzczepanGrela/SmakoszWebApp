using FluentValidation;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Auth.Commands.Register;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator(IValidationConfigProvider config)
    {
        var usernameMin = config.GetInt("auth.username_min_length", 3);
        var usernameMax = config.GetInt("auth.username_max_length", 30);
        var passwordMin = config.GetInt("auth.password_min_length", 8);
        var passwordMax = config.GetInt("auth.password_max_length", 128);
        var requireDigit = config.GetBool("auth.password_require_digit", true);
        var requireSpecial = config.GetBool("auth.password_require_special", true);

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Nazwa użytkownika jest wymagana")
            .MinimumLength(usernameMin).WithMessage($"Nazwa użytkownika musi mieć co najmniej {usernameMin} znaki")
            .MaximumLength(usernameMax).WithMessage($"Nazwa użytkownika może mieć maksymalnie {usernameMax} znaków")
            .Matches(@"^[a-zA-Z0-9_.-]+$").WithMessage("Nazwa użytkownika może zawierać tylko litery, cyfry, kropki, myślniki i podkreślenia");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email jest wymagany")
            .EmailAddress().WithMessage("Nieprawidłowy format adresu email");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Hasło jest wymagane")
            .MinimumLength(passwordMin).WithMessage($"Hasło musi mieć co najmniej {passwordMin} znaków")
            .MaximumLength(passwordMax).WithMessage($"Hasło może mieć maksymalnie {passwordMax} znaków");

        if (requireDigit)
        {
            RuleFor(x => x.Password)
                .Matches(@"\d").WithMessage("Hasło musi zawierać co najmniej jedną cyfrę");
        }

        if (requireSpecial)
        {
            RuleFor(x => x.Password)
                .Matches(@"[^a-zA-Z0-9]").WithMessage("Hasło musi zawierać co najmniej jeden znak specjalny");
        }
    }
}
