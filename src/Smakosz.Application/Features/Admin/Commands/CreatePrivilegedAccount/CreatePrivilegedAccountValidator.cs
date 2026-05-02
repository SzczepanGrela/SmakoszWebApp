using FluentValidation;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreatePrivilegedAccount;

public class CreatePrivilegedAccountValidator : AbstractValidator<CreatePrivilegedAccountCommand>
{
    public CreatePrivilegedAccountValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email jest wymagany")
            .EmailAddress().WithMessage("Nieprawidłowy format email")
            .MaximumLength(255).WithMessage("Email może mieć maksymalnie 255 znaków");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Nazwa użytkownika jest wymagana")
            .MinimumLength(3).WithMessage("Nazwa użytkownika musi mieć co najmniej 3 znaki")
            .MaximumLength(50).WithMessage("Nazwa użytkownika może mieć maksymalnie 50 znaków");

        RuleFor(x => x.Role)
            .Must(r => r == UserRole.Admin || r == UserRole.Moderator)
            .WithMessage("Można utworzyc tylko konto Admin lub Moderator");
    }
}
