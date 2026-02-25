using FluentValidation;

namespace Smakosz.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email jest wymagany")
            .EmailAddress().WithMessage("Nieprawidłowy format adresu email");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kod weryfikacyjny jest wymagany");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Nowe hasło jest wymagane")
            .MinimumLength(8).WithMessage("Hasło musi mieć co najmniej 8 znaków");
    }
}
