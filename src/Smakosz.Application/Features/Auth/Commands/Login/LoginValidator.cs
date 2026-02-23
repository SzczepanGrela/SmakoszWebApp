using FluentValidation;

namespace Smakosz.Application.Features.Auth.Commands.Login;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email jest wymagany")
            .EmailAddress().WithMessage("Nieprawidłowy format adresu email");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Hasło jest wymagane");
    }
}
