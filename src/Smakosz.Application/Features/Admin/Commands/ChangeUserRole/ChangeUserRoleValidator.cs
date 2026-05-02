using FluentValidation;

namespace Smakosz.Application.Features.Admin.Commands.ChangeUserRole;

public class ChangeUserRoleValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleValidator()
    {
        RuleFor(x => x.NewRole)
            .IsInEnum().WithMessage("Nieprawidłowa rola");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Uzasadnienie może mieć maksymalnie 500 znaków")
            .When(x => x.Reason is not null);
    }
}
