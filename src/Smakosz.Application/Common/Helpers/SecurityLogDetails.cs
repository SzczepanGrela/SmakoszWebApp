using System.Text.Json;

namespace Smakosz.Application.Common.Helpers;

public static class SecurityLogDetails
{
    public static string BannedIdentifier() => """{"reason":"banned_identifier"}""";

    public static string AccountLocked() => """{"reason":"account_locked"}""";

    public static string AdminAction(int? adminId) =>
        JsonSerializer.Serialize(new { admin_id = adminId });

    public static string RoleChange(string from, string to, string? reason, int? adminId) =>
        JsonSerializer.Serialize(new { from, to, reason, admin_id = adminId });

    public static string PrivilegedAccountInvite(int? adminId, string role) =>
        JsonSerializer.Serialize(new { admin_id = adminId, role });

    public static string AcceptInviteFlow() => """{"flow":"accept_invite"}""";

    public static string GdprAccountDeletion() => """{"action":"gdpr_account_deletion"}""";

    public static string PasswordChanged() => """{"action":"password_changed"}""";

    public static string LoginSuccess() => "{}";

    public static string AutoBanPrivilegedBruteForce(int threshold, int banHours) =>
        JsonSerializer.Serialize(new { kind = "auto_ban_priv", threshold, ban_hours = banHours });
}
