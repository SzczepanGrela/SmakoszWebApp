using FluentValidation;

namespace Smakosz.Application.Features.Admin.Commands.UpdateRestaurantAdmin;

public class UpdateRestaurantAdminValidator : AbstractValidator<UpdateRestaurantAdminCommand>
{
    public UpdateRestaurantAdminValidator()
    {
        RuleFor(x => x.Name)
            .MinimumLength(2).WithMessage("Nazwa restauracji musi mieć co najmniej 2 znaki")
            .MaximumLength(200).WithMessage("Nazwa restauracji może mieć maksymalnie 200 znaków")
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Opis może mieć maksymalnie 5000 znaków")
            .When(x => x.Description is not null);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Nieprawidłowy format adresu email")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .Matches(@"^[\d\s\+\-\(\)]{7,20}$").WithMessage("Nieprawidłowy format numeru telefonu")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Website)
            .Matches(@"^https?://.+").WithMessage("Adres strony musi zaczynać się od http:// lub https://")
            .When(x => !string.IsNullOrEmpty(x.Website));

        RuleFor(x => x.PriceLevel)
            .InclusiveBetween(1, 4).WithMessage("Poziom cenowy musi być między 1 a 4")
            .When(x => x.PriceLevel.HasValue);

        RuleFor(x => x.CuisineTypeId)
            .GreaterThan(0).WithMessage("Identyfikator kuchni musi być większy od 0")
            .When(x => x.CuisineTypeId.HasValue);

        RuleFor(x => x.PostalCode)
            .Matches(@"^\d{2}-\d{3}$").WithMessage("Kod pocztowy musi być w formacie XX-XXX")
            .When(x => !string.IsNullOrEmpty(x.PostalCode));

        RuleFor(x => x.CityId)
            .GreaterThan(0).WithMessage("Identyfikator miasta musi być większy od 0")
            .When(x => x.CityId.HasValue);

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Wersja musi być większa od 0");
    }
}
