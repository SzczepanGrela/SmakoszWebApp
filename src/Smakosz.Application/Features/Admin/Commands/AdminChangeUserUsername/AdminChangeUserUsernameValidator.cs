using FluentValidation;

namespace Smakosz.Application.Features.Admin.Commands.AdminChangeUserUsername;

public class AdminChangeUserUsernameValidator : AbstractValidator<AdminChangeUserUsernameCommand>
{
    public AdminChangeUserUsernameValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.NewUsername).NotEmpty().Length(3, 50).Matches("^[a-zA-Z0-9._-]+$");
    }
}
