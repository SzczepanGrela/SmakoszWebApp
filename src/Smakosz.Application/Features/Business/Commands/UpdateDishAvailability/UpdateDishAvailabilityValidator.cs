using FluentValidation;

namespace Smakosz.Application.Features.Business.Commands.UpdateDishAvailability;

public class UpdateDishAvailabilityValidator : AbstractValidator<UpdateDishAvailabilityCommand>
{
    public UpdateDishAvailabilityValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Identyfikator dania jest wymagany");
    }
}
