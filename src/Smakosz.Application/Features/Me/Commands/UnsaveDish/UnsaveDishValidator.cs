using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.UnsaveDish;

public class UnsaveDishValidator : AbstractValidator<UnsaveDishCommand>
{
    public UnsaveDishValidator()
    {
        RuleFor(x => x.DishSlug)
            .NotEmpty().WithMessage("Slug dania jest wymagany");
    }
}
