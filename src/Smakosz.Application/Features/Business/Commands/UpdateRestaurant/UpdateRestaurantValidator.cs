using FluentValidation;

namespace Smakosz.Application.Features.Business.Commands.UpdateRestaurant;

public class UpdateRestaurantValidator : AbstractValidator<UpdateRestaurantCommand>
{
    public UpdateRestaurantValidator()
    {
        RuleFor(x => x.Name)
            .MinimumLength(2).WithMessage("Nazwa restauracji musi mieć co najmniej 2 znaki")
            .MaximumLength(200).WithMessage("Nazwa restauracji może mieć maksymalnie 200 znaków")
            .When(x => x.Name is not null);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Nieprawidłowy format adresu email")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .Matches(@"^[\d\s\+\-\(\)]{7,20}$").WithMessage("Nieprawidłowy format numeru telefonu")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.CityId)
            .GreaterThan(0).WithMessage("Identyfikator miasta musi być większy od 0")
            .When(x => x.CityId.HasValue);
    }
}
