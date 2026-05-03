using FluentValidation;

namespace Smakosz.Application.Features.Restaurants.Commands.RequestRestaurantClaim;

public class RequestRestaurantClaimValidator : AbstractValidator<RequestRestaurantClaimCommand>
{
    public RequestRestaurantClaimValidator()
    {
        RuleFor(x => x.RestaurantPublicId).NotEmpty();
        RuleFor(x => x.Justification).NotEmpty().MinimumLength(10).MaximumLength(2000);
    }
}
