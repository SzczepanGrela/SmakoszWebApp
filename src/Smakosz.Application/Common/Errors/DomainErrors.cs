using ErrorOr;

namespace Smakosz.Application.Common.Errors;

public static class DomainErrors
{
    public static class Auth
    {
        public static readonly Error InvalidCredentials =
            Error.Unauthorized("AUTH_INVALID_CREDENTIALS", "Nieprawidłowy email lub hasło");

        public static readonly Error EmailAlreadyExists =
            Error.Conflict("AUTH_EMAIL_EXISTS", "Email jest już zarejestrowany");

        public static readonly Error UsernameAlreadyExists =
            Error.Conflict("AUTH_USERNAME_EXISTS", "Nazwa użytkownika jest już zajęta");

        public static readonly Error EmailNotVerified =
            Error.Forbidden("AUTH_EMAIL_NOT_VERIFIED", "Email nie został zweryfikowany");

        public static readonly Error AccountBanned =
            Error.Forbidden("AUTH_ACCOUNT_BANNED", "Konto zostało zablokowane");

        public static readonly Error AccountInactive =
            Error.Forbidden("AUTH_ACCOUNT_INACTIVE", "Konto jest nieaktywne");

        public static readonly Error InvalidRefreshToken =
            Error.Unauthorized("AUTH_INVALID_REFRESH_TOKEN", "Nieprawidłowy lub wygasły refresh token");

        public static readonly Error InvalidVerificationCode =
            Error.Validation("AUTH_INVALID_VERIFICATION_CODE", "Nieprawidłowy lub wygasły kod weryfikacyjny");
    }

    public static class Restaurant
    {
        public static readonly Error NotFound =
            Error.NotFound("RESTAURANT_NOT_FOUND", "Restauracja nie została znaleziona");
    }

    public static class Dish
    {
        public static readonly Error NotFound =
            Error.NotFound("DISH_NOT_FOUND", "Danie nie zostało znalezione");
    }

    public static class Review
    {
        public static readonly Error NotFound =
            Error.NotFound("REVIEW_NOT_FOUND", "Recenzja nie została znaleziona");

        public static readonly Error AlreadyExists =
            Error.Conflict("REVIEW_ALREADY_EXISTS", "Już dodałeś recenzję tego dania");

        public static readonly Error NotOwner =
            Error.Forbidden("REVIEW_NOT_OWNER", "Nie jesteś autorem tej recenzji");
    }

    public static class User
    {
        public static readonly Error NotFound =
            Error.NotFound("USER_NOT_FOUND", "Użytkownik nie został znaleziony");
    }
}
