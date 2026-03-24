using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.UnfavoriteRestaurant;

public class UnfavoriteRestaurantValidator : AbstractValidator<UnfavoriteRestaurantCommand>
{
    public UnfavoriteRestaurantValidator()
    {
        RuleFor(x => x.RestaurantSlug)
            .NotEmpty().WithMessage("Slug restauracji jest wymagany");
    }
}
