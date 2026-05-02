using FluentValidation;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Auth.Commands.AcceptInvite;

public class AcceptInviteValidator : AbstractValidator<AcceptInviteCommand>
{
    public AcceptInviteValidator(IValidationConfigProvider config)
    {
        var passwordMin = config.GetInt("auth.password_min_length", 8);
        var passwordMax = config.GetInt("auth.password_max_length", 128);
        var requireDigit = config.GetBool("auth.password_require_digit", true);
        var requireSpecial = config.GetBool("auth.password_require_special", true);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email jest wymagany")
            .EmailAddress().WithMessage("Nieprawidłowy format adresu email");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kod zaproszenia jest wymagany");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Nowe hasło jest wymagane")
            .MinimumLength(passwordMin).WithMessage($"Hasło musi mieć co najmniej {passwordMin} znaków")
            .MaximumLength(passwordMax).WithMessage($"Hasło może mieć maksymalnie {passwordMax} znaków");

        if (requireDigit)
        {
            RuleFor(x => x.NewPassword)
                .Matches(@"\d").WithMessage("Haslo musi zawierac co najmniej jedna cyfre");
        }

        if (requireSpecial)
        {
            RuleFor(x => x.NewPassword)
                .Matches(@"[^a-zA-Z0-9]").WithMessage("Hasło musi zawierac co najmniej jeden znak specjalny");
        }
    }
}
