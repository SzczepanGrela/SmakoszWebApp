using FluentValidation;

namespace Smakosz.Application.Features.Admin.Commands.ChangeUserRole;

public class ChangeUserRoleValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleValidator()
    {
        RuleFor(x => x.NewRole)
            .IsInEnum().WithMessage("Nieprawidlowa rola");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Uzasadnienie moze miec maksymalnie 500 znakow")
            .When(x => x.Reason is not null);
    }
}
