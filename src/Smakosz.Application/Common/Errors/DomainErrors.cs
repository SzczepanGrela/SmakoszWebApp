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
            Error.Forbidden("AUTH_IDENTIFIER_BANNED", "Rejestracja z tego adresu nie jest możliwa");

        public static readonly Error AccountInactive =
            Error.Forbidden("AUTH_ACCOUNT_INACTIVE", "Konto jest nieaktywne");

        public static readonly Error AccountLocked =
            Error.Forbidden("AUTH_ACCOUNT_LOCKED", "Konto zostało tymczasowo zablokowane z powodu zbyt wielu nieudanych prób logowania");

        public static readonly Error InvalidRefreshToken =
            Error.Unauthorized("AUTH_INVALID_REFRESH_TOKEN", "Nieprawidłowy lub wygasły refresh token");

        public static readonly Error InvalidVerificationCode =
            Error.Validation("AUTH_INVALID_VERIFICATION_CODE", "Nieprawidłowy lub wygasły kod weryfikacyjny");

        public static readonly Error TwoFactorRequired =
            Error.Forbidden("AUTH_2FA_REQUIRED", "Wymagana weryfikacja dwuetapowa");

        public static readonly Error TwoFactorAlreadyEnabled =
            Error.Conflict("AUTH_2FA_ALREADY_ENABLED", "Weryfikacja dwuetapowa jest już włączona");

        public static readonly Error TwoFactorNotEnabled =
            Error.Conflict("AUTH_2FA_NOT_ENABLED", "Weryfikacja dwuetapowa nie jest włączona");
    }

    public static class Restaurant
    {
        public static readonly Error NotFound =
            Error.NotFound("RESTAURANT_NOT_FOUND", "Restauracja nie została znaleziona");

        public static readonly Error VersionMismatch =
            Error.Conflict("RESTAURANT_VERSION_MISMATCH", "Dane restauracji zostały zmienione przez innego użytkownika. Odśwież stronę i spróbuj ponownie.");

        public static readonly Error InvalidStatusTransition =
            Error.Validation("RESTAURANT_INVALID_STATUS_TRANSITION", "Niedozwolona zmiana statusu restauracji");

        public static readonly Error SameStatus =
            Error.Validation("RESTAURANT_SAME_STATUS", "Restauracja ma już ten status");
    }

    public static class Dish
    {
        public static readonly Error NotFound =
            Error.NotFound("DISH_NOT_FOUND", "Danie nie zostało znalezione");

        public static readonly Error InvalidCategory =
            Error.Validation("DISH_INVALID_CATEGORY", "Wybrana kategoria dania nie istnieje lub jest nieaktywna");

        public static readonly Error InvalidModerationStatus =
            Error.Validation("DISH_INVALID_MODERATION_STATUS", "Nieprawidlowy status moderacji dania");

        public static Error InvalidTag(string category, string name) =>
            Error.Validation("DISH_INVALID_TAG", $"Tag '{name}' nie istnieje w kategorii '{category}'");
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

        public static readonly Error PhotoLimitExceeded =
            Error.Validation("MEDIA_PHOTO_LIMIT_EXCEEDED", "Osiągnięto limit zdjęć dla tej recenzji");
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

        public static readonly Error CannotChangeOwnRole =
            Error.Validation("ADMIN_CANNOT_CHANGE_OWN_ROLE", "Administrator nie może zmienić własnej roli");

        public static readonly Error CannotDemoteLastAdmin =
            Error.Validation("ADMIN_CANNOT_DEMOTE_LAST_ADMIN", "Nie można zdegradować ostatniego administratora");

        public static readonly Error EmailAlreadyExists =
            Error.Conflict("ADMIN_EMAIL_ALREADY_EXISTS", "Konto z tym adresem email już istnieje");

        public static readonly Error UsernameAlreadyExists =
            Error.Conflict("ADMIN_USERNAME_ALREADY_EXISTS", "Konto z tą nazwą użytkownika już istnieje");

        public static readonly Error InvalidRoleForPrivilegedAccount =
            Error.Validation("ADMIN_INVALID_ROLE", "Można utworzyć tylko konto Admin lub Moderator");

        public static readonly Error BulkLimitExceeded =
            Error.Validation("ADMIN_BULK_LIMIT_EXCEEDED", "Liczba elementów przekracza dozwolony limit operacji zbiorczej");

        public static readonly Error BulkEmpty =
            Error.Validation("ADMIN_BULK_EMPTY", "Lista elementów do moderacji nie może być pusta");

        public static readonly Error BulkReasonRequired =
            Error.Validation("ADMIN_BULK_REASON_REQUIRED", "Odrzucenie wymaga wybrania co najmniej jednego powodu lub wpisania uwagi moderatora");
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

    public static class Tag
    {
        public static readonly Error NotFound =
            Error.NotFound("TAG_NOT_FOUND", "Tag nie został znaleziony");

        public static readonly Error AlreadyExists =
            Error.Conflict("TAG_ALREADY_EXISTS", "Tag o podanej nazwie już istnieje");
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

    public static class ReviewLike
    {
        public static readonly Error CannotLikeOwnReview =
            Error.Validation("REVIEW_LIKE_CANNOT_LIKE_OWN", "Nie można polubić własnej recenzji");
    }

    public static class Account
    {
        public static readonly Error IsRestaurantOwner =
            Error.Forbidden("ACCOUNT_IS_RESTAURANT_OWNER",
                "Nie można usunąć konta właściciela restauracji. Skontaktuj się z administracją.");
    }

    public static class Captcha
    {
        public static readonly Error VerificationFailed =
            Error.Validation("CAPTCHA_FAILED", "Weryfikacja CAPTCHA nie powiodła się");
    }

    public static class BannedIdentifier
    {
        public static readonly Error NotFound =
            Error.NotFound("BANNED_IDENTIFIER_NOT_FOUND", "Zbanowany identyfikator nie zostal znaleziony");

        public static readonly Error AlreadyExists =
            Error.Conflict("BANNED_IDENTIFIER_ALREADY_EXISTS", "Ban na ten identyfikator juz istnieje");

        public static readonly Error InvalidFormat =
            Error.Validation("BANNED_IDENTIFIER_INVALID_FORMAT", "Nieprawidlowy format wartosci dla wybranego typu");
    }

    public static class ForbiddenWord
    {
        public static readonly Error UsernameContainsForbiddenWord =
            Error.Validation("FORBIDDEN_WORD_USERNAME", "Nazwa użytkownika zawiera niedozwolone słowo");

        public static readonly Error ContentContainsForbiddenWord =
            Error.Validation("FORBIDDEN_WORD_CONTENT", "Treść zawiera niedozwolone słowo");

        public static readonly Error NotFound =
            Error.NotFound("FORBIDDEN_WORD_NOT_FOUND", "Zakazane słowo nie zostało znalezione");

        public static readonly Error AlreadyExists =
            Error.Conflict("FORBIDDEN_WORD_ALREADY_EXISTS", "Zakazane słowo już istnieje");

        public static readonly Error InvalidRegex =
            Error.Validation("FORBIDDEN_WORD_INVALID_REGEX", "Nieprawidłowe wyrażenie regularne");
    }

    public static class RejectionReason
    {
        public static readonly Error NotFound =
            Error.NotFound("REJECTION_REASON_NOT_FOUND", "Powód odrzucenia nie został znaleziony");

        public static readonly Error CodeAlreadyExists =
            Error.Conflict("REJECTION_REASON_CODE_EXISTS", "Powód odrzucenia o podanym kodzie już istnieje");

        public static readonly Error LabelAlreadyExists =
            Error.Conflict("REJECTION_REASON_LABEL_EXISTS", "Powód odrzucenia o podanej etykiecie już istnieje");

        public static readonly Error InvalidCategory =
            Error.Validation("REJECTION_REASON_INVALID_CATEGORY", "Nieprawidłowa kategoria powodu odrzucenia");

        public static readonly Error InvalidCode =
            Error.Validation("REJECTION_REASON_INVALID_CODE", "Nieprawidłowy format kodu powodu odrzucenia");

        public static readonly Error RejectionRequiresReason =
            Error.Validation("REJECTION_REASON_REQUIRED", "Odrzucenie wymaga podania co najmniej jednego powodu lub uwagi moderatora");

        public static readonly Error CategoryMismatch =
            Error.Validation("REJECTION_REASON_CATEGORY_MISMATCH", "Wybrany powód odrzucenia nie pasuje do typu moderowanej treści");

        public static readonly Error InactiveReason =
            Error.Validation("REJECTION_REASON_INACTIVE", "Wybrany powód odrzucenia jest nieaktywny");

        public static readonly Error UnknownReasonCode =
            Error.Validation("REJECTION_REASON_UNKNOWN_CODE", "Nieznany kod powodu odrzucenia");
    }
}
