using FluentValidation;

namespace Smakosz.Application.Features.Admin.Commands.BanUser;

public class BanUserValidator : AbstractValidator<BanUserCommand>
{
    public BanUserValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Identyfikator użytkownika jest wymagany");
    }
}
