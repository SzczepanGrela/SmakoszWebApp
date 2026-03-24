using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.FavoriteRestaurant;

public class FavoriteRestaurantValidator : AbstractValidator<FavoriteRestaurantCommand>
{
    public FavoriteRestaurantValidator()
    {
        RuleFor(x => x.RestaurantSlug)
            .NotEmpty().WithMessage("Slug restauracji jest wymagany");
    }
}
