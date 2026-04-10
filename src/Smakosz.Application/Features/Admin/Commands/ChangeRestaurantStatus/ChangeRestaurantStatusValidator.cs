using FluentValidation;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ChangeRestaurantStatus;

public class ChangeRestaurantStatusValidator : AbstractValidator<ChangeRestaurantStatusCommand>
{
    public ChangeRestaurantStatusValidator()
    {
        RuleFor(x => x.NewStatus).IsInEnum()
            .WithMessage("Nieprawidłowy status restauracji");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Powód jest wymagany")
            .MinimumLength(5).WithMessage("Powód musi mieć co najmniej 5 znaków")
            .MaximumLength(1000).WithMessage("Powód może mieć maksymalnie 1000 znaków")
            .When(x => x.NewStatus is RestaurantStatus.Suspended or RestaurantStatus.ClosedPermanently);
    }
}
