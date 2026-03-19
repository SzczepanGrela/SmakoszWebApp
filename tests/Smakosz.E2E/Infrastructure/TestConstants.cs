namespace Smakosz.E2E.Infrastructure;

public static class TestConstants
{
    public const string ApiBaseUrl = "http://localhost:5000";
    public const string ClientBaseUrl = "http://localhost:5003";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("SMAKOSZ_E2E_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=smakosz_e2e;Username=postgres;Password=***REMOVED***";

    public const string UserEmail = "jan.kowalski@gmail.com";
    public const string UserPassword = "TestHaslo123!";
    public const string UserUsername = "jan-kowalski";

    public const string User2Email = "anna.nowak@wp.pl";
    public const string User2Username = "anna-nowak";

    public const string BusinessEmail = "marco.rossi@pizzeriaroma.pl";
    public const string BusinessPassword = "TestHaslo123!";
    public const string BusinessUsername = "marco-rossi";

    public const string AdminEmail = "admin@smakosz.pl";
    public const string AdminPassword = "TestHaslo123!";

    public const string ModeratorEmail = "moderator@smakosz.test";
    public const string ModeratorPassword = "TestHaslo123!";

    public const string BannedUsername = "zbanowany";
    public const string BannedEmail = "zbanowany@smakosz.test";
    public const string BannedPassword = "TestHaslo123!";
    public const int BannedUserId = 5;

    public static string JwtSecret =>
        Environment.GetEnvironmentVariable("SMAKOSZ_JWT_SECRET")
        ?? "***REMOVED***";
    public const string JwtIssuer = "Smakosz.API";
    public const string JwtAudience = "Smakosz.Client";
}
