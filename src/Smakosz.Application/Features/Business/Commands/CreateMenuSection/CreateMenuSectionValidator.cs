using FluentValidation;

namespace Smakosz.Application.Features.Business.Commands.CreateMenuSection;

public class CreateMenuSectionValidator : AbstractValidator<CreateMenuSectionCommand>
{
    public CreateMenuSectionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa sekcji jest wymagana")
            .MaximumLength(100).WithMessage("Nazwa sekcji może mieć maksymalnie 100 znaków");
    }
}
