using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.FollowUser;

public class FollowUserValidator : AbstractValidator<FollowUserCommand>
{
    public FollowUserValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug użytkownika jest wymagany");
    }
}
