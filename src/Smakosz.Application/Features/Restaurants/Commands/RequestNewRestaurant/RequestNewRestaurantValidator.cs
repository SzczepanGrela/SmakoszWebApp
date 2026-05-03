using FluentValidation;

namespace Smakosz.Application.Features.Restaurants.Commands.RequestNewRestaurant;

public class RequestNewRestaurantValidator : AbstractValidator<RequestNewRestaurantCommand>
{
    public RequestNewRestaurantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MinimumLength(5).MaximumLength(300);
        When(x => x.Email is not null, () => RuleFor(x => x.Email!).EmailAddress());
        When(x => x.Phone is not null, () => RuleFor(x => x.Phone!).Matches(@"^[\d\s\+\-\(\)]+$"));
        RuleFor(x => x.Description!).MaximumLength(2000).When(x => x.Description is not null);
    }
}
