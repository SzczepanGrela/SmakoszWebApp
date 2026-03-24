using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.UpdateProfile;

public class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.Username)
            .MinimumLength(3).WithMessage("Nazwa użytkownika musi mieć co najmniej 3 znaki")
            .MaximumLength(30).WithMessage("Nazwa użytkownika może mieć maksymalnie 30 znaków")
            .Matches(@"^[a-zA-Z0-9_.-]+$").WithMessage("Nazwa użytkownika może zawierać tylko litery, cyfry, kropki, myślniki i podkreślenia")
            .When(x => x.Username is not null);
    }
}
