using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.UnfollowUser;

public class UnfollowUserValidator : AbstractValidator<UnfollowUserCommand>
{
    public UnfollowUserValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug użytkownika jest wymagany");
    }
}
