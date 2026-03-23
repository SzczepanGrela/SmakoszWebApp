namespace Smakosz.Domain.Enums;

public enum SecurityEventType
{
    FailedLogin,
    BlockedIp,
    SuspiciousActivity,
    PasswordReset,
    BannedRegistration,
    PasswordChanged,
    AccountDeleted
}
