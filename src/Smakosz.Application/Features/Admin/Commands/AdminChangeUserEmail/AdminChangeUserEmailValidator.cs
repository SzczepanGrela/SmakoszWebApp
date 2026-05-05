using FluentValidation;

namespace Smakosz.Application.Features.Admin.Commands.AdminChangeUserEmail;

public class AdminChangeUserEmailValidator : AbstractValidator<AdminChangeUserEmailCommand>
{
    public AdminChangeUserEmailValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.NewEmail).NotEmpty().EmailAddress().MaximumLength(100);
    }
}
