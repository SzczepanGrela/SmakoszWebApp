using FluentValidation;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreatePrivilegedAccount;

public class CreatePrivilegedAccountValidator : AbstractValidator<CreatePrivilegedAccountCommand>
{
    public CreatePrivilegedAccountValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email jest wymagany")
            .EmailAddress().WithMessage("Nieprawidlowy format email")
            .MaximumLength(255).WithMessage("Email moze miec maksymalnie 255 znakow");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Nazwa uzytkownika jest wymagana")
            .MinimumLength(3).WithMessage("Nazwa uzytkownika musi miec co najmniej 3 znaki")
            .MaximumLength(50).WithMessage("Nazwa uzytkownika moze miec maksymalnie 50 znakow");

        RuleFor(x => x.Role)
            .Must(r => r == UserRole.Admin || r == UserRole.Moderator)
            .WithMessage("Mozna utworzyc tylko konto Admin lub Moderator");
    }
}
