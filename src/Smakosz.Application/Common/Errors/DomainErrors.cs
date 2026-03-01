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

        public static readonly Error IdentifierBanned =
            Error.Forbidden("AUTH_IDENTIFIER_BANNED", "Rejestracja z tego adresu nie jest mozliwa");

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

        public static readonly Error UsernameAlreadyExists =
            Error.Conflict("USER_USERNAME_EXISTS", "Nazwa użytkownika jest już zajęta");
    }

    public static class Session
    {
        public static readonly Error NotFound =
            Error.NotFound("SESSION_NOT_FOUND", "Sesja nie została znaleziona");

        public static readonly Error CannotRevokeCurrent =
            Error.Validation("SESSION_CANNOT_REVOKE_CURRENT", "Nie można unieważnić bieżącej sesji");
    }

    public static class Follow
    {
        public static readonly Error AlreadyFollowing =
            Error.Conflict("FOLLOW_ALREADY_FOLLOWING", "Już obserwujesz tego użytkownika");

        public static readonly Error NotFollowing =
            Error.NotFound("FOLLOW_NOT_FOLLOWING", "Nie obserwujesz tego użytkownika");

        public static readonly Error CannotFollowSelf =
            Error.Validation("FOLLOW_CANNOT_FOLLOW_SELF", "Nie można obserwować samego siebie");
    }

    public static class SavedDish
    {
        public static readonly Error AlreadySaved =
            Error.Conflict("SAVED_DISH_ALREADY_SAVED", "Danie jest już zapisane");

        public static readonly Error NotSaved =
            Error.NotFound("SAVED_DISH_NOT_SAVED", "Danie nie jest zapisane");
    }

    public static class FavoriteRestaurant
    {
        public static readonly Error AlreadyFavorited =
            Error.Conflict("FAVORITE_RESTAURANT_ALREADY_FAVORITED", "Restauracja jest już w ulubionych");

        public static readonly Error NotFavorited =
            Error.NotFound("FAVORITE_RESTAURANT_NOT_FAVORITED", "Restauracja nie jest w ulubionych");
    }

    public static class Notification
    {
        public static readonly Error NotFound =
            Error.NotFound("NOTIFICATION_NOT_FOUND", "Powiadomienie nie zostało znalezione");
    }

    public static class Report
    {
        public static readonly Error NotFound =
            Error.NotFound("REPORT_NOT_FOUND", "Zgłoszenie nie zostało znalezione");

        public static readonly Error InvalidEntityType =
            Error.Validation("REPORT_INVALID_ENTITY_TYPE", "Nieprawidłowy typ zgłaszanej encji");

        public static readonly Error InvalidStatus =
            Error.Validation("REPORT_INVALID_STATUS", "Nieprawidłowy status zgłoszenia");

        public static readonly Error InvalidReasonCode =
            Error.Validation("REPORT_INVALID_REASON_CODE", "Nieprawidłowy kod powodu zgłoszenia");
    }

    public static class Media
    {
        public static readonly Error FileTooLarge =
            Error.Validation("MEDIA_FILE_TOO_LARGE", "Plik jest zbyt duży (max 5 MB)");

        public static readonly Error InvalidFormat =
            Error.Validation("MEDIA_INVALID_FORMAT", "Nieprawidłowy format pliku (dozwolone: jpg, png, webp)");
    }

    public static class Content
    {
        public static readonly Error NotFound =
            Error.NotFound("CONTENT_NOT_FOUND", "Strona nie została znaleziona");
    }

    public static class Business
    {
        public static readonly Error RestaurantExists =
            Error.Conflict("BUSINESS_RESTAURANT_EXISTS", "Restauracja o podanej nazwie lub adresie email już istnieje");

        public static readonly Error NotVerified =
            Error.Forbidden("BUSINESS_NOT_VERIFIED", "Konto restauracji nie zostało jeszcze zweryfikowane");

        public static readonly Error NotOwner =
            Error.Forbidden("BUSINESS_NOT_OWNER", "Nie jesteś właścicielem tej restauracji");

        public static readonly Error RegistrationPending =
            Error.Conflict("BUSINESS_REGISTRATION_PENDING", "Rejestracja w trakcie weryfikacji");
    }

    public static class Admin
    {
        public static readonly Error Forbidden =
            Error.Forbidden("ADMIN_FORBIDDEN", "Brak uprawnień do wykonania tej operacji");
    }

    public static class Social
    {
        public static readonly Error UserRoleOnly =
            Error.Forbidden("SOCIAL_USER_ROLE_ONLY", "Tylko użytkownicy z rolą 'user' mogą wykonywać akcje społecznościowe");
    }

    public static class City
    {
        public static readonly Error NotFound =
            Error.NotFound("CITY_NOT_FOUND", "Miasto nie zostało znalezione");

        public static readonly Error AlreadyExists =
            Error.Conflict("CITY_ALREADY_EXISTS", "Miasto o podanej nazwie już istnieje");
    }

    public static class Ingredient
    {
        public static readonly Error NotFound =
            Error.NotFound("INGREDIENT_NOT_FOUND", "Składnik nie został znaleziony");

        public static readonly Error AlreadyExists =
            Error.Conflict("INGREDIENT_ALREADY_EXISTS", "Składnik o podanej nazwie już istnieje");
    }

    public static class MenuSection
    {
        public static readonly Error NotFound =
            Error.NotFound("MENU_SECTION_NOT_FOUND", "Sekcja menu nie została znaleziona");

        public static readonly Error NotOwner =
            Error.Forbidden("MENU_SECTION_NOT_OWNER", "Nie jesteś właścicielem tej sekcji menu");
    }

    public static class Correction
    {
        public static readonly Error NotFound =
            Error.NotFound("CORRECTION_NOT_FOUND", "Zgłoszenie korekty nie zostało znalezione");
    }

    public static class Ticket
    {
        public static readonly Error NotFound =
            Error.NotFound("TICKET_NOT_FOUND", "Zgłoszenie nie zostało znalezione");

        public static readonly Error InvalidStatus =
            Error.Validation("TICKET_INVALID_STATUS", "Nieprawidłowy status zgłoszenia");
    }

    public static class EditRequest
    {
        public static readonly Error NotFound =
            Error.NotFound("EDIT_REQUEST_NOT_FOUND", "Wniosek o edycję nie został znaleziony");
    }

    public static class Job
    {
        public static readonly Error NotFound =
            Error.NotFound("JOB_NOT_FOUND", "Zadanie nie zostało znalezione");
    }

    public static class Photo
    {
        public static readonly Error NotFound =
            Error.NotFound("PHOTO_NOT_FOUND", "Zdjęcie nie zostało znalezione");
    }

    public static class ForbiddenWord
    {
        public static readonly Error UsernameContainsForbiddenWord =
            Error.Validation("FORBIDDEN_WORD_USERNAME", "Nazwa użytkownika zawiera niedozwolone słowo");

        public static readonly Error ContentContainsForbiddenWord =
            Error.Validation("FORBIDDEN_WORD_CONTENT", "Treść zawiera niedozwolone słowo");
    }
}
