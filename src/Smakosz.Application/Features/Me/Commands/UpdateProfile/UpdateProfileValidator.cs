using FluentValidation;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.UpdateProfile;

public class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator(IValidationConfigProvider config)
    {
        var usernameMin = config.GetInt("auth.username_min_length", 3);
        var usernameMax = config.GetInt("auth.username_max_length", 30);

        RuleFor(x => x.Username)
            .MinimumLength(usernameMin).WithMessage($"Nazwa użytkownika musi mieć co najmniej {usernameMin} znaki")
            .MaximumLength(usernameMax).WithMessage($"Nazwa użytkownika może mieć maksymalnie {usernameMax} znaków")
            .Matches(@"^[a-zA-Z0-9_.-]+$").WithMessage("Nazwa użytkownika może zawierać tylko litery, cyfry, kropki, myślniki i podkreślenia")
            .When(x => x.Username is not null);
    }
}
