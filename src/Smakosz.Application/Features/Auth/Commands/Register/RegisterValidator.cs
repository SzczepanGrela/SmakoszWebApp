using FluentValidation;

namespace Smakosz.Application.Features.Auth.Commands.Register;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Nazwa użytkownika jest wymagana")
            .MinimumLength(3).WithMessage("Nazwa użytkownika musi mieć co najmniej 3 znaki")
            .MaximumLength(30).WithMessage("Nazwa użytkownika może mieć maksymalnie 30 znaków")
            .Matches(@"^[a-zA-Z0-9_.-]+$").WithMessage("Nazwa użytkownika może zawierać tylko litery, cyfry, kropki, myślniki i podkreślenia");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email jest wymagany")
            .EmailAddress().WithMessage("Nieprawidłowy format adresu email");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Hasło jest wymagane")
            .MinimumLength(8).WithMessage("Hasło musi mieć co najmniej 8 znaków");
    }
}
