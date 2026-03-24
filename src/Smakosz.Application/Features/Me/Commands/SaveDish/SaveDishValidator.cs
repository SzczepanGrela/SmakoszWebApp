using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.SaveDish;

public class SaveDishValidator : AbstractValidator<SaveDishCommand>
{
    public SaveDishValidator()
    {
        RuleFor(x => x.DishSlug)
            .NotEmpty().WithMessage("Slug dania jest wymagany");
    }
}
